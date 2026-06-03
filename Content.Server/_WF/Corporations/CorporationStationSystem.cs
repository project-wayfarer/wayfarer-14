using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.Corporations;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Tag;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._WF.Corporations;

/// <summary>
/// Manages persistent corporation player stations: loading at round start, saving every 4 hours and at round end.
/// </summary>
public sealed class CorporationStationSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly SharedShuttleSystem _shuttle = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private ISawmill _log = default!;

    /// <summary>Maps corpId → loaded grid EntityUid for all active stations this round.</summary>
    private readonly Dictionary<int, EntityUid> _activeStations = new();

    /// <summary>Maps corpId → whether the station FTL beacon is visible to shuttle consoles.</summary>
    private readonly Dictionary<int, bool> _stationVisible = new();

    private TimeSpan _nextAutosave = TimeSpan.MaxValue;

    private static readonly ResPath TemplatePath = new("/Maps/_WF/PlayerStation/playerStation.yml");

    /// <summary>Cost in spesos to purchase a corporation station.</summary>
    public const int StationCost = 5_000_000;

    public override void Initialize()
    {
        base.Initialize();
        _log = _logManager.GetSawmill("wf.corp_stations");

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextAutosave)
            return;

        _nextAutosave = _timing.CurTime + TimeSpan.FromHours(4);
        SaveAllStations(stripBlacklist: false);
    }

    private async void OnRoundStart(RoundStartingEvent ev)
    {
        _activeStations.Clear();
        _stationVisible.Clear();
        _nextAutosave = _timing.CurTime + TimeSpan.FromHours(4);

        List<(int corpId, string stationName, string savePath)> toLoad = new();
        try
        {
            var allCorps = await _db.GetAllCorporations();
            foreach (var corp in allCorps)
            {
                var station = await _db.GetCorporationStation(corp.Id);
                if (station != null)
                    toLoad.Add((corp.Id, station.StationName, station.SavePath));
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load corp stations from DB: {ex}");
            return;
        }

        foreach (var (corpId, stationName, savePath) in toLoad)
        {
            SpawnStation(corpId, stationName, savePath, RandomOffset());
        }
    }

    private async void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New == GameRunLevel.PostRound)
        {
            await ChargeUpkeep();
            SaveAllStations(stripBlacklist: true);
        }
    }

    private async void OnPlayerAttached(PlayerAttachedEvent args)
    {
        // Only check during an active round
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        if (!TryComp<ActorComponent>(args.Entity, out var actor))
            return;

        var userId = actor.PlayerSession.UserId.UserId;

        WayfarerCorporation? corp;
        try
        {
            corp = await _db.GetCorporationForPlayer(userId);
        }
        catch (Exception ex)
        {
            _log.Error($"OnPlayerAttached: failed to fetch corp for {userId}: {ex}");
            return;
        }

        if (corp == null)
            return;

        var station = await _db.GetCorporationStation(corp.Id);
        if (station == null)
            return;

        var upkeep = GetUpkeepCost(corp.Id);
        if (upkeep is null or 0)
            return;

        if (corp.Balance < upkeep.Value)
        {
            var message = Loc.GetString("corp-notify-low-balance-warning",
                ("corpName", corp.Name),
                ("balance", corp.Balance.ToString("N0")),
                ("upkeep", upkeep.Value.ToString("N0")));

            var wrapped = Loc.GetString("chat-manager-server-wrap-message",
                ("message", FormattedMessage.EscapeText(message)));
            _chat.ChatMessageToOne(ChatChannel.Server, message, wrapped, EntityUid.Invalid,
                false, actor.PlayerSession.Channel, colorOverride: Color.FromHex("#FF9900"));
        }
    }

    // Public API

    /// <summary>
    /// Admin shortcut: grants a station to a corporation for free, creating the DB record and spawning the grid.
    /// Returns false if the corp already has a station.
    /// </summary>
    public async Task<bool> GrantStation(int corpId, string stationName)
    {
        var existing = await _db.GetCorporationStation(corpId);
        if (existing != null)
            return false;

        var savePath = $"corp_stations/corp_{corpId}.yml";
        await _db.CreateCorporationStation(corpId, stationName, savePath);

        SpawnStation(corpId, stationName, savePath, RandomOffset());
        return true;
    }

    /// <summary>
    /// Purchases a station for the given corporation: withdraws the cost, creates the DB record, and spawns the grid.
    /// Returns false if the corp already has a station or cannot afford it.
    /// </summary>
    public async Task<bool> PurchaseStation(int corpId, string stationName)
    {
        var existing = await _db.GetCorporationStation(corpId);
        if (existing != null)
            return false;

        if (!await _db.TryWithdrawFromCorporation(corpId, StationCost))
            return false;

        var savePath = $"corp_stations/corp_{corpId}.yml";
        await _db.CreateCorporationStation(corpId, stationName, savePath);

        SpawnStation(corpId, stationName, savePath, RandomOffset());
        return true;
    }

    /// <summary>Toggles shuttle-console visibility of the station FTL beacon. Returns the new visibility state.</summary>
    public bool ToggleStationVisibility(int corpId)
    {
        var visible = !IsStationVisible(corpId);
        _stationVisible[corpId] = visible;

        if (!_activeStations.TryGetValue(corpId, out var gridUid))
            return visible;

        if (visible)
            _shuttle.RemoveIFFFlag(gridUid, IFFFlags.Hide);
        else
            _shuttle.AddIFFFlag(gridUid, IFFFlags.Hide);

        return visible;
    }

    /// <summary>Returns whether the station is currently visible on shuttle scanners.</summary>
    public bool IsStationVisible(int corpId)
        => _stationVisible.TryGetValue(corpId, out var v) && v;

    /// <summary>
    /// Returns the upkeep cost in spesos for the given corporation's active station,
    /// calculated as appraised grid value × the upkeep multiplier CVAR.
    /// Returns null if the station is not currently loaded.
    /// </summary>
    public int? GetUpkeepCost(int corpId)
    {
        if (!_activeStations.TryGetValue(corpId, out var gridUid))
            return null;
        if (!EntityManager.EntityExists(gridUid))
            return null;

        var multiplier = _cfg.GetCVar(WFCCVars.StationUpkeepMultiplier);
        var appraised = _pricing.AppraiseGrid(gridUid);
        return (int)(appraised * multiplier);
    }

    /// <summary>Returns the world coordinates of the active station grid, or null if not loaded.</summary>
    public Vector2? GetStationCoordinates(int corpId)
    {
        if (!_activeStations.TryGetValue(corpId, out var gridUid))
            return null;
        if (!EntityManager.EntityExists(gridUid))
            return null;
        return _xforms.GetWorldPosition(gridUid);
    }

    // Internals

    /// <summary>
    /// Loads a corporation station grid into the world.
    /// Loads from the saved user-data file if it exists, otherwise from the template.
    /// </summary>
    private EntityUid? SpawnStation(int corpId, string stationName, string savePath, Vector2 offset)
    {
        var saveResPath = new ResPath($"/{savePath}");
        var opts = DeserializationOptions.Default with { InitializeMaps = true };

        if (!_map.TryGetMap(_gameTicker.DefaultMap, out var sectorMapUid))
        {
            _log.Error($"Could not find sector map to spawn station for corp {corpId}");
            return null;
        }
        var mapId = _gameTicker.DefaultMap;

        EntityUid gridUid;

        if (_res.UserData.Exists(saveResPath))
        {
            // Saved file is category: Grid (written by TrySaveGrid) — position is baked in, no extra offset.
            if (!_loader.TryLoadGrid(mapId, saveResPath, out var gridEnt, opts, offset: Vector2.Zero))
            {
                _log.Error($"Failed to load saved station for corp {corpId} from {saveResPath}");
                return null;
            }
            gridUid = gridEnt.Value;
        }
        else
        {
            // Template is category: Grid
            if (!_loader.TryLoadGrid(mapId, TemplatePath, out var gridEnt, opts, offset: offset))
            {
                _log.Error($"Failed to load station template for corp {corpId} from {TemplatePath}");
                return null;
            }
            gridUid = gridEnt.Value;
        }

        // Name the grid.
        _meta.SetEntityName(gridUid, stationName);

        _activeStations[corpId] = gridUid;
        _stationVisible.TryAdd(corpId, false);
        // Start hidden by default — add IFF with Hide flag.
        var iff = EnsureComp<IFFComponent>(gridUid);
        _shuttle.AddIFFFlag(gridUid, IFFFlags.Hide, iff);
        _log.Info($"Spawned station '{stationName}' for corp {corpId} at offset {offset}");
        return gridUid;
    }

    private async Task ChargeUpkeep()
    {
        var evicted = new List<int>();

        foreach (var (corpId, gridUid) in _activeStations)
        {
            if (!EntityManager.EntityExists(gridUid))
                continue;

            var cost = GetUpkeepCost(corpId);
            if (cost is null or 0)
                continue;

            try
            {
                var withdrawn = await _db.TryWithdrawFromCorporation(corpId, cost.Value);
                if (withdrawn)
                {
                    _log.Info($"Charged {cost.Value} spesos upkeep for corp {corpId}");
                    await NotifyCorpLeadership(corpId, Loc.GetString("corp-notify-upkeep-charged",
                        ("amount", cost.Value.ToString("N0"))));
                }
                else
                {
                    _log.Warning($"Corp {corpId} could not afford station upkeep of {cost.Value} spesos — removing station");
                    await NotifyCorpLeadership(corpId, Loc.GetString("corp-notify-upkeep-evicted",
                        ("amount", cost.Value.ToString("N0"))));
                    evicted.Add(corpId);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to charge upkeep for corp {corpId}: {ex}");
            }
        }

        foreach (var corpId in evicted)
        {
            await EvictStation(corpId);
        }
    }

    public async Task EvictStation(int corpId)
    {
        // Remove DB record
        try
        {
            await _db.DeleteCorporationStation(corpId);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to delete DB station record for corp {corpId}: {ex}");
        }

        // Archive the save file instead of deleting it
        var saveResPath = new ResPath($"/corp_stations/corp_{corpId}.yml");
        if (_res.UserData.Exists(saveResPath))
        {
            try
            {
                var deletedDir = new ResPath("/corp_stations/deleted");
                _res.UserData.CreateDir(deletedDir);

                var archiveName = $"corp_{corpId}_{_gameTicker.RoundId}.yml";
                var archivePath = deletedDir / archiveName;

                // Copy the file to the archive location
                using (var src = _res.UserData.OpenRead(saveResPath))
                using (var dst = _res.UserData.OpenWrite(archivePath))
                    src.CopyTo(dst);

                _res.UserData.Delete(saveResPath);
                _log.Info($"Archived evicted station for corp {corpId} to {archivePath}");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to archive save file for corp {corpId}: {ex}");
            }
        }

        // Delete the active grid entity from the world
        if (_activeStations.TryGetValue(corpId, out var gridUid) && EntityManager.EntityExists(gridUid))
            EntityManager.DeleteEntity(gridUid);

        _activeStations.Remove(corpId);
        _stationVisible.Remove(corpId);
    }

    /// <summary>
    /// Sends a server message to all online corp owners and managers.
    /// </summary>
    private async Task NotifyCorpLeadership(int corpId, string message)
    {
        WayfarerCorporation? corp;
        try
        {
            corp = await _db.GetCorporationById(corpId);
        }
        catch (Exception ex)
        {
            _log.Error($"NotifyCorpLeadership: failed to fetch corp {corpId}: {ex}");
            return;
        }

        if (corp == null)
            return;

        foreach (var member in corp.Members)
        {
            if ((CorporationRank)member.Rank < CorporationRank.Manager)
                continue;

            if (!_playerManager.TryGetSessionById(new NetUserId(member.UserId), out var session) || session == null)
                continue;

            var wrapped = Loc.GetString("chat-manager-server-wrap-message",
                ("message", FormattedMessage.EscapeText(message)));
            _chat.ChatMessageToOne(ChatChannel.Server, message, wrapped, EntityUid.Invalid,
                false, session.Channel, colorOverride: Color.FromHex("#FF69B4"));
        }
    }

    public void SaveAllStations(bool stripBlacklist = false)
    {
        foreach (var (corpId, gridUid) in _activeStations)
        {
            if (!EntityManager.EntityExists(gridUid))
                continue;

            if (stripBlacklist)
                StripBlacklistedEntities(gridUid);

            var savePath = new ResPath($"/corp_stations/corp_{corpId}.yml");
            if (_loader.TrySaveGrid(gridUid, savePath))
                _log.Info($"Saved station for corp {corpId}");
            else
                _log.Error($"Failed to save station for corp {corpId}");
        }
    }

    /// <summary>Saves a single corporation's active station to disk. Returns false if not active this round.</summary>
    public bool SaveStation(int corpId)
    {
        if (!_activeStations.TryGetValue(corpId, out var gridUid) || !EntityManager.EntityExists(gridUid))
            return false;
        var savePath = new ResPath($"/corp_stations/corp_{corpId}.yml");
        if (_loader.TrySaveGrid(gridUid, savePath))
        {
            _log.Info($"Admin saved station for corp {corpId}");
            return true;
        }
        _log.Error($"Admin: failed to save station for corp {corpId}");
        return false;
    }

    /// <summary>Returns whether a corp has an active (spawned) station this round.</summary>
    public bool HasActiveStation(int corpId) => _activeStations.ContainsKey(corpId);

    /// <summary>
    /// Returns the filenames (not full paths) of archived station saves for the given corp
    /// stored in <c>/corp_stations/deleted/</c>, e.g. <c>["corp_3_55.yml"]</c>.
    /// </summary>
    public List<string> GetArchivedStationFiles(int corpId)
    {
        var deletedDir = new ResPath("/corp_stations/deleted");
        var prefix = $"corp_{corpId}_";
        var result = new List<string>();

        try
        {
            foreach (var entry in _res.UserData.DirectoryEntries(deletedDir))
            {
                if (entry.StartsWith(prefix) && entry.EndsWith(".yml"))
                    result.Add(entry);
            }
        }
        catch
        {
            // Directory doesn't exist yet — return empty
        }

        return result;
    }

    /// <summary>
    /// Restores an archived station save for a corporation:
    /// copies the archive file back to the active save location, creates the DB record, and spawns the grid.
    /// Returns false if the corp already has a station or the archive file doesn't exist.
    /// </summary>
    public async Task<bool> RecoverStation(int corpId, string archiveFileName, string stationName)
    {
        // Don't overwrite an existing active station
        var existing = await _db.GetCorporationStation(corpId);
        if (existing != null)
            return false;

        var archivePath = new ResPath($"/corp_stations/deleted/{archiveFileName}");
        if (!_res.UserData.Exists(archivePath))
        {
            _log.Warning($"RecoverStation: archive file {archivePath} not found for corp {corpId}");
            return false;
        }

        var savePath = $"corp_stations/corp_{corpId}.yml";
        var saveResPath = new ResPath($"/{savePath}");

        try
        {
            _res.UserData.CreateDir(new ResPath("/corp_stations"));
            using (var src = _res.UserData.OpenRead(archivePath))
            using (var dst = _res.UserData.OpenWrite(saveResPath))
                src.CopyTo(dst);

            _res.UserData.Delete(archivePath);
        }
        catch (Exception ex)
        {
            _log.Error($"RecoverStation: failed to restore archive for corp {corpId}: {ex}");
            return false;
        }

        await _db.CreateCorporationStation(corpId, stationName, savePath);
        SpawnStation(corpId, stationName, savePath, RandomOffset());
        _log.Info($"Recovered station '{stationName}' for corp {corpId} from archive {archiveFileName}");
        return true;
    }

    /// <summary>
    /// Deletes all entities on <paramref name="gridUid"/> whose prototype or tags appear in the
    /// <c>corpStationSaveBlacklist</c> prototype, so they are not persisted in the save file.
    /// </summary>
    private void StripBlacklistedEntities(EntityUid gridUid)
    {
        if (!_proto.TryIndex<CorpStationSaveBlacklistPrototype>("Default", out var blacklist))
            return;

        if (blacklist.Prototypes.Count == 0 && blacklist.Tags.Count == 0)
            return;

        var toDelete = new List<EntityUid>();
        var query = AllEntityQuery<TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var xform, out var meta))
        {
            if (xform.GridUid != gridUid)
                continue;

            // Check prototype blacklist.
            var protoId = meta.EntityPrototype?.ID;
            if (protoId != null && blacklist.Prototypes.Contains((EntProtoId) protoId))
            {
                toDelete.Add(uid);
                continue;
            }

            // Check tag blacklist.
            foreach (var tag in blacklist.Tags)
            {
                if (_tag.HasTag(uid, tag))
                {
                    toDelete.Add(uid);
                    break;
                }
            }
        }

        foreach (var uid in toDelete)
            Del(uid);

        if (toDelete.Count > 0)
            _log.Debug($"Stripped {toDelete.Count} blacklisted entities from {ToPrettyString(gridUid)} before save");
    }

    private static Vector2 RandomOffset()
    {
        var rng = new Random();
        var angle = rng.NextDouble() * Math.PI * 2;
        var dist = rng.NextDouble() * 2000 + 5000; // 5000–7000 units from center
        return new Vector2((float)(Math.Cos(angle) * dist), (float)(Math.Sin(angle) * dist));
    }
}
