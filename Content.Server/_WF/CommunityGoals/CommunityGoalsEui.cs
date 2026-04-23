using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Shared._WF.CommunityGoals;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Log;

namespace Content.Server._WF.CommunityGoals;

public sealed class CommunityGoalsEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystems = default!;

    private CommunityGoalsSystem _goals = default!;
    private GameTicker _gameTicker = default!;
    private readonly ISawmill _sawmill;

    public CommunityGoalsEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("admin.community_goals");
    }

    public override EuiStateBase GetNewState()
    {
        // Synchronous path: state is fetched on Opened() and refreshed after each mutation.
        // We keep a cached copy and push it immediately; mutations call RefreshAsync().
        return new CommunityGoalsEuiState(_cachedGoals, _cachedRound);
    }

    private int _cachedRound;

    private List<CommunityGoalData> _cachedGoals = new();

    public override async void Opened()
    {
        base.Opened();
        _goals = _entitySystems.GetEntitySystem<CommunityGoalsSystem>();
        _gameTicker = _entitySystems.GetEntitySystem<GameTicker>();
        await RefreshAsync();
    }

    private static CommunityGoalData ToData(WayfarerCommunityGoal g) => new()
    {
        Id = g.Id,
        Title = g.Title,
        Description = g.Description,
        StartRound = g.StartRound,
        EndRound = g.EndRound,
        IsActive = g.IsActive,
        Requirements = g.Requirements.Select(r => new CommunityGoalRequirementData
        {
            Id = r.Id,
            EntityPrototypeId = r.EntityPrototypeId,
            DisplayName = r.DisplayName,
            RequiredAmount = r.RequiredAmount,
            CurrentAmount = r.CurrentAmount,
        }).ToList(),
    };

    private async Task RefreshAsync()
    {
        var goals = await _db.GetAllCommunityGoals();
        if (IsShutDown)
            return;
        _cachedGoals = goals.Select(ToData).ToList();
        _cachedRound = _gameTicker.RoundId;
        StateDirty();
    }

    /// <summary>
    /// Refreshes the EUI's own goal cache AND tells CommunityGoalsSystem to reload
    /// its active-goals cache, which raises CommunityGoalsUpdatedEvent and pushes
    /// fresh state to all open in-game consoles.
    /// </summary>
    private async Task RefreshAllAsync()
    {
        await RefreshAsync();
        if (IsShutDown)
            return;
        await _goals.RefreshActiveGoals();
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} tried to use community goals EUI without Admin flag");
            return;
        }

        switch (msg)
        {
            case CreateCommunityGoalMessage create:
                await _db.CreateCommunityGoal(create.Title, create.Description, create.StartRound, create.EndRound);
                _sawmill.Info($"Admin {Player.Name} created community goal '{create.Title}'");
                break;

            case UpdateCommunityGoalMessage update:
                await _db.UpdateCommunityGoal(update.GoalId, update.Title, update.Description, update.StartRound, update.EndRound, update.IsActive);
                _sawmill.Info($"Admin {Player.Name} updated community goal #{update.GoalId}");
                break;

            case DeleteCommunityGoalMessage delete:
                await _db.DeleteCommunityGoal(delete.GoalId);
                _sawmill.Info($"Admin {Player.Name} deleted community goal #{delete.GoalId}");
                break;

            case AddCommunityGoalRequirementMessage addReq:
                await _db.AddCommunityGoalRequirement(addReq.GoalId, addReq.EntityPrototypeId, addReq.DisplayName, addReq.RequiredAmount);
                _sawmill.Info($"Admin {Player.Name} added requirement '{addReq.EntityPrototypeId}' to goal #{addReq.GoalId}");
                break;

            case RemoveCommunityGoalRequirementMessage removeReq:
                await _db.RemoveCommunityGoalRequirement(removeReq.RequirementId);
                _sawmill.Info($"Admin {Player.Name} removed requirement #{removeReq.RequirementId}");
                break;

            case UpdateCommunityGoalRequirementMessage updateReq:
                await _db.UpdateCommunityGoalRequirement(updateReq.RequirementId, updateReq.RequiredAmount);
                _sawmill.Info($"Admin {Player.Name} updated requirement #{updateReq.RequirementId} required amount to {updateReq.RequiredAmount}");
                break;
        }

        await RefreshAllAsync();
    }
}
