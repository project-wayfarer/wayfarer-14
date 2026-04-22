using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Research.Disk;
using Content.Server.GameTicking;
using Content.Server._NF.RoundNotifications.Events;
using Content.Shared._WF.CommunityGoals;
using Content.Shared.Stacks;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.CommunityGoals;

/// <summary>
/// Raised on the server whenever the cached active community goals list changes
/// (contributions recorded, admin edits applied, or round-start load).
/// Subscribe to this to know when to push fresh UI state to in-game consoles.
/// </summary>
public sealed class CommunityGoalsUpdatedEvent : EntityEventArgs { }

/// <summary>
/// Tracks which community goals are active for the current round and
/// provides the API used by future in-game terminals to submit contributions.
/// </summary>
public sealed class CommunityGoalsSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

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
        RaiseLocalEvent(new CommunityGoalsUpdatedEvent());
    }

    /// <summary>
    /// Records a contribution of <paramref name="amount"/> units for every active requirement
    /// whose EntityPrototypeId matches <paramref name="entityPrototypeId"/> (exact or same stack type).
    /// Returns the number of requirements updated.
    /// </summary>
    public async Task<int> RecordContribution(string entityPrototypeId, long amount)
    {
        var itemStackType = GetProtoStackTypeId(entityPrototypeId);
        var updated = 0;

        foreach (var goal in _activeGoals)
        {
            foreach (var req in goal.Requirements)
            {
                if (!MatchesRequirement(entityPrototypeId, itemStackType, req.EntityPrototypeId))
                    continue;

                await _db.AddCommunityGoalContribution(req.Id, amount);
                req.CurrentAmount += amount;
                updated++;

                _sawmill.Debug($"Contribution: +{amount} '{entityPrototypeId}' → goal #{goal.Id} req #{req.Id} " +
                               $"({req.CurrentAmount}/{req.RequiredAmount})");
            }
        }

        if (updated > 0)
            RaiseLocalEvent(new CommunityGoalsUpdatedEvent());

        return updated;
    }

    /// <summary>
    /// Returns true if an item with <paramref name="itemProtoId"/> (and optional
    /// <paramref name="itemStackTypeId"/>) satisfies a requirement defined as
    /// <paramref name="reqProtoId"/>.
    /// Matches by exact prototype ID, shared stack type (so SheetSteel10
    /// satisfies a SheetSteel requirement), or shared research-disk category
    /// (any ResearchDisk variant satisfies a ResearchDisk requirement).
    /// </summary>
    public bool MatchesRequirement(string itemProtoId, string? itemStackTypeId, string reqProtoId)
    {
        if (itemProtoId.Equals(reqProtoId, StringComparison.OrdinalIgnoreCase))
            return true;

        // Stack-type matching (e.g. SheetSteel10 matches a SheetSteel requirement)
        if (itemStackTypeId != null)
        {
            var reqStackType = GetProtoStackTypeId(reqProtoId);
            if (reqStackType != null && reqStackType.Equals(itemStackTypeId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Research-disk matching: any ResearchDisk variant matches any other ResearchDisk requirement
        if (IsResearchDiskProto(itemProtoId) && IsResearchDiskProto(reqProtoId))
            return true;

        return false;
    }

    /// <summary>
    /// Returns true if the given entity prototype has a <c>ResearchDiskComponent</c>.
    /// </summary>
    public bool IsResearchDiskProto(string protoId)
    {
        if (!_protoManager.TryIndex<EntityPrototype>(protoId, out var proto))
            return false;
        return proto.TryGetComponent<ResearchDiskComponent>(out _);
    }

    /// <summary>
    /// Returns the StackTypeId defined on the given entity prototype, or null if it has none.
    /// </summary>
    public string? GetProtoStackTypeId(string protoId)
    {
        if (!_protoManager.TryIndex<EntityPrototype>(protoId, out var proto))
            return null;

        return proto.TryGetComponent<StackComponent>(out var sc) ? sc.StackTypeId : null;
    }

    /// <summary>
    /// Records a contribution of <paramref name="amount"/> units directly to the specific
    /// requirement identified by <paramref name="requirementId"/>, bypassing prototype matching.
    /// Used by the targeted per-requirement contribute button.
    /// </summary>
    public async Task RecordContributionToRequirement(int requirementId, long amount)
    {
        await _db.AddCommunityGoalContribution(requirementId, amount);

        foreach (var goal in _activeGoals)
        {
            foreach (var req in goal.Requirements)
            {
                if (req.Id != requirementId)
                    continue;

                req.CurrentAmount += amount;
                _sawmill.Debug($"Targeted contribution: +{amount} → req #{requirementId} " +
                               $"({req.CurrentAmount}/{req.RequiredAmount})");
                break;
            }
        }

        RaiseLocalEvent(new CommunityGoalsUpdatedEvent());
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

        RaiseLocalEvent(new CommunityGoalsUpdatedEvent());
    }
}
