using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._WF.Administration.CentComSpawn;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Clothing;
using Content.Shared.Eui;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Server.StationRecords;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.Station.Components;

namespace Content.Server._WF.Administration.CentComSpawn;

public sealed class CentComSpawnEui : BaseEui
{
    private static readonly ProtoId<JobPrototype> Job = CentComSpawnJob.Id;

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;

    private readonly EntityCoordinates _coordinates;
    private readonly EntityUid _station;

    public CentComSpawnEui(EntityCoordinates coordinates, EntityUid station)
    {
        IoCManager.InjectDependencies(this);
        _coordinates = coordinates;
        _station = station;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState() =>
        new CentComSpawnEuiState((HumanoidCharacterProfile) _prefs.GetPreferences(Player.UserId).SelectedCharacter);

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not CentComSpawnEuiMsg spawn
            || spawn.Loadout.Role != LoadoutSystem.GetJobPrototype(Job)
            || !_adminManager.HasAdminFlag(Player, AdminFlags.Spawn))
            return;

        var prefs = _prefs.GetPreferences(Player.UserId);
        var selected = (HumanoidCharacterProfile) prefs.SelectedCharacter;

        _ = _prefs.SetProfile(Player.UserId, prefs.SelectedCharacterIndex, selected.WithLoadout(spawn.Loadout));
        var profile = (HumanoidCharacterProfile) _prefs.GetPreferences(Player.UserId).SelectedCharacter;

        var gameTicker = _entityManager.System<GameTicker>();
        if (!gameTicker.UserHasJoinedGame(Player))
            gameTicker.PlayerJoinGame(Player, silent: true);

        var mindSystem = _entityManager.System<SharedMindSystem>();
        var mind = mindSystem.CreateMind(Player.UserId, profile.Name);
        mindSystem.SetUserId(mind, Player.UserId);

        var mob = _entityManager.System<StationSpawningSystem>()
            .SpawnPlayerMob(_coordinates, Job, profile, _entityManager.HasComponent<StationJobsComponent>(_station) ? _station : null, session: Player);

        mindSystem.TransferTo(mind, mob, mind: mind.Comp);
        _entityManager.System<SharedRoleSystem>().MindAddJobRole(mind, jobPrototype: Job);

        gameTicker.PlayersJoinedRoundNormally++;
        var ev = new PlayerSpawnCompleteEvent(mob,
            Player,
            Job,
            lateJoin: true,
            silent: true,
            joinOrder: gameTicker.PlayersJoinedRoundNormally,
            _station,
            profile);
        _entityManager.EventBus.RaiseLocalEvent(mob, ev, true);

        if (!_entityManager.HasComponent<StationRecordsComponent>(_station))
        {
            _entityManager.System<InventorySystem>().TryGetSlotEntity(mob, "id", out var idUid);
            _entityManager.TryGetComponent<FingerprintComponent>(mob, out var fingerprint);
            _entityManager.TryGetComponent<DnaComponent>(mob, out var dna);
            _entityManager.System<StationRecordsSystem>()
                .TryCreateSectorRecord(mob, idUid, profile, Job, fingerprint?.Fingerprint, dna?.DNA);
        }

        _adminLogger.Add(LogType.LateJoin,
            LogImpact.High,
            $"{Player.Name} spawned as {_entityManager.ToPrettyString(mob):entity} as a {Job:jobName} on {_entityManager.ToPrettyString(_station):station}.");

        Close();
    }
}

[AdminCommand(AdminFlags.Spawn)]
public sealed class CentComSpawnCommand : LocalizedEntityCommands
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override string Command => "wfcentcomspawn";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        EntityCoordinates coordinates;
        EntityUid station;
        var location = string.Join(' ', args).Trim();

        if (location.Equals("here", StringComparison.OrdinalIgnoreCase))
        {
            if (player.AttachedEntity is not { } admin
                || !_transform.TryGetMapOrGridCoordinates(admin, out var here)
                || _station.GetOwningStation(admin) is not { } hereStation)
            {
                shell.WriteError(Loc.GetString("cmd-wfcentcomspawn-not-on-station"));
                return;
            }

            coordinates = here.Value;
            station = hereStation;
        }
        else
        {
            var matches = FindStations(location);
            if (matches.Count != 1)
            {
                shell.WriteError(Loc.GetString("cmd-wfcentcomspawn-station-not-found", ("name", location)));
                return;
            }

            station = matches[0];
            var spawnPoints = new List<EntityCoordinates>();
            var query = EntityManager.EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                if (spawnPoint.SpawnType == SpawnPointType.LateJoin && _station.GetOwningStation(uid, xform) == station)
                    spawnPoints.Add(xform.Coordinates);
            }

            if (spawnPoints.Count == 0)
            {
                shell.WriteError(Loc.GetString("cmd-wfcentcomspawn-no-spawn-point", ("name", location)));
                return;
            }

            coordinates = _random.Pick(spawnPoints);
        }

        _euiManager.OpenEui(new CentComSpawnEui(coordinates, station), player);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var names = _station.GetStations().Select(s => EntityManager.GetComponent<MetaDataComponent>(s).EntityName);
        return CompletionResult.FromHintOptions(names.Prepend("here"), Loc.GetString("cmd-wfcentcomspawn-hint"));
    }

    private List<EntityUid> FindStations(string query)
    {
        var matches = new List<EntityUid>();
        foreach (var station in _station.GetStations())
        {
            var name = EntityManager.GetComponent<MetaDataComponent>(station).EntityName;
            if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
                return [station];

            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                matches.Add(station);
        }

        return matches;
    }
}
