using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server._NF.RoundNotifications.Events;
using Content.Shared._WF.CommunityGoals;
using Robust.Shared.Log;

namespace Content.Server._WF.CommunityGoals;

/// <summary>
/// Tracks which community goals are active for the current round and
/// provides the API used by future in-game terminals to submit contributions.
/// </summary>
public sealed class CommunityGoalsSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    /// <summary>
    /// Goals that are active for the current round, loaded at round start.
    /// This is an in-memory cache; all mutations are persisted to the DB immediately.
    /// </summary>
    private List<CommunityGoalData> _activeGoals = new();

    public IReadOnlyList<CommunityGoalData> ActiveGoals => _activeGoals;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("community_goals");
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private async void OnRoundStarted(RoundStartedEvent ev)
    {
        var roundId = _gameTicker.RoundId;
        var goals = await _db.GetActiveCommunityGoals(roundId);

        _activeGoals = goals.Select(g => new CommunityGoalData
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
        }).ToList();

        _sawmill.Info($"Loaded {_activeGoals.Count} active community goal(s) for round {roundId}.");
    }

    /// <summary>
    /// Records a contribution of <paramref name="amount"/> units for every active requirement
    /// whose EntityPrototypeId matches <paramref name="entityPrototypeId"/>.
    /// Returns the number of requirements updated.
    /// </summary>
    public async Task<int> RecordContribution(string entityPrototypeId, long amount)
    {
        var updated = 0;

        foreach (var goal in _activeGoals)
        {
            foreach (var req in goal.Requirements)
            {
                if (!req.EntityPrototypeId.Equals(entityPrototypeId, StringComparison.OrdinalIgnoreCase))
                    continue;

                await _db.AddCommunityGoalContribution(req.Id, amount);
                req.CurrentAmount += amount;
                updated++;

                _sawmill.Debug($"Contribution: +{amount} '{entityPrototypeId}' → goal #{goal.Id} req #{req.Id} " +
                               $"({req.CurrentAmount}/{req.RequiredAmount})");
            }
        }

        return updated;
    }

    /// <summary>
    /// Gets a fresh snapshot of all active goals directly from the database,
    /// refreshing <see cref="ActiveGoals"/> in the process.
    /// </summary>
    public async Task RefreshActiveGoals()
    {
        var roundId = _gameTicker.RoundId;
        var goals = await _db.GetActiveCommunityGoals(roundId);

        _activeGoals = goals.Select(g => new CommunityGoalData
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
        }).ToList();
    }
}
