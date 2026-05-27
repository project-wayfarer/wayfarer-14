using System.Linq;
using Content.Server._WF.Corporations;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared._WF.Corporations;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Server._WF.Corporations.AdminEui;

[UsedImplicitly]
public sealed class CorpAdminEui : BaseEui
{
    private static readonly ISawmill Log = Logger.GetSawmill("corp.admin.eui");

    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private CorporationStationSystem _stationSystem = default!;
    private CorpAdminEuiState _cachedState = new() { Corporations = new() };

    public CorpAdminEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        _stationSystem = _entMan.System<CorporationStationSystem>();
        RefreshState();
    }

    public override EuiStateBase GetNewState() => _cachedState;

    private async void RefreshState()
    {
        if (IsShutDown) return;
        try
        {
        var corps = await _db.GetAllCorporations();
        var list = new List<CorpAdminCorpData>();

        foreach (var corp in corps.OrderBy(c => c.Name))
        {
            var station = await _db.GetCorporationStation(corp.Id);

            list.Add(new CorpAdminCorpData
            {
                Id = corp.Id,
                Name = corp.Name,
                Description = corp.Description,
                Privacy = (CorporationPrivacy) corp.Privacy,
                Balance = corp.Balance,
                Members = corp.Members.Select(m => new CorpAdminMemberData
                {
                    UserId = m.UserId.ToString(),
                    DisplayName = m.DisplayName,
                    Rank = (CorporationRank) m.Rank,
                }).ToList(),
                Station = station == null ? null : new CorpAdminStationData
                {
                    StationName = station.StationName,
                    SavePath = station.SavePath,
                    ActiveThisRound = _stationSystem.HasActiveStation(corp.Id),
                },
                ArchivedStationFiles = _stationSystem.GetArchivedStationFiles(corp.Id),
            });
        }

        _cachedState = new CorpAdminEuiState { Corporations = list };
        if (!IsShutDown)
            StateDirty();
        }
        catch (Exception ex)
        {
            Log.Error($"CorpAdminEui RefreshState failed: {ex}");
        }
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }

        HandleAsync(msg);
    }

    private async void HandleAsync(EuiMessageBase msg)
    {
        try
        {
        switch (msg)
        {
            case CorpAdminEuiMsg.Refresh:
                break; // just fall through to RefreshState

            case CorpAdminEuiMsg.SetBalance setBalance:
                await _db.SetCorporationBalance(setBalance.CorporationId, setBalance.NewBalance);
                break;

            case CorpAdminEuiMsg.SetDescription setDesc:
                await _db.UpdateCorporationDescription(setDesc.CorporationId, setDesc.Description);
                break;

            case CorpAdminEuiMsg.SetPrivacy setPrivacy:
                await _db.UpdateCorporationPrivacy(setPrivacy.CorporationId, (int) setPrivacy.Privacy);
                break;

            case CorpAdminEuiMsg.KickMember kick:
                if (Guid.TryParse(kick.UserId, out var kickGuid))
                    await _db.RemoveCorporationMember(kick.CorporationId, kickGuid);
                break;

            case CorpAdminEuiMsg.SetMemberRank setRank:
                if (Guid.TryParse(setRank.UserId, out var rankGuid))
                    await _db.UpdateCorporationMemberRank(setRank.CorporationId, rankGuid, (int) setRank.Rank);
                break;

            case CorpAdminEuiMsg.DeleteCorporation delete:
                await _db.DeleteCorporation(delete.CorporationId);
                break;

            case CorpAdminEuiMsg.EvictStation evict:
                await _stationSystem.EvictStation(evict.CorporationId);
                break;

            case CorpAdminEuiMsg.SaveStation save:
                _stationSystem.SaveStation(save.CorporationId);
                break;

            case CorpAdminEuiMsg.GrantStation grant:
                await _stationSystem.GrantStation(grant.CorporationId, grant.StationName);
                break;

            case CorpAdminEuiMsg.CreateCorporation create:
                if (!string.IsNullOrWhiteSpace(create.Name))
                    await _db.AdminCreateCorporation(create.Name, create.Description, (int) create.Privacy);
                break;

            case CorpAdminEuiMsg.AddMember add:
                var displayName = _players.TryGetSessionById(new NetUserId(add.UserId), out var session)
                    ? session.Name
                    : add.UserId.ToString();
                await _db.AddCorporationMember(add.CorporationId, add.UserId, displayName, (int) CorporationRank.Member);
                break;

            case CorpAdminEuiMsg.RecoverStation recover:
                if (!string.IsNullOrWhiteSpace(recover.ArchiveFileName))
                    await _stationSystem.RecoverStation(recover.CorporationId, recover.ArchiveFileName, recover.StationName);
                break;
        }

        if (!IsShutDown)
            RefreshState();
        }
        catch (Exception ex)
        {
            Log.Error($"CorpAdminEui HandleAsync failed: {ex}");
        }
    }
}
