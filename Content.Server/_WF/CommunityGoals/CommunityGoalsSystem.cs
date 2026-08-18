using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Research.Disk;
using Content.Server.GameTicking;
using Content.Server._NF.RoundNotifications.Events;
using Content.Server._WF.CommunityGoals.Components;
using Content.Shared._WF.CommunityGoals;
using Content.Shared.Mobs;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Log;
using Robust.Shared.Player;
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
    [Dependency] private readonly TagSystem _tags = default!;

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
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    /// <summary>
    /// Watches for mobs dying and records a contribution toward any active kill-order
    /// requirement matching the dead mob's prototype.
    /// </summary>
    private async void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        var protoId = MetaData(args.Target).EntityPrototype?.ID;
        if (protoId == null)
            return;

        Guid? playerUserId = null;
        string? characterName = null;
        if (args.Origin is { } origin && TryComp<ActorComponent>(origin, out var actor))
        {
            playerUserId = actor.PlayerSession.UserId;
            characterName = MetaData(origin).EntityName;
        }

        await RecordKill(args.Target, protoId, playerUserId, characterName);
    }

    /// <summary>
    /// Records a kill of <paramref name="entityPrototypeId"/> toward every active kill-order
    /// requirement that targets it. Each requirement is only ever credited once per
    /// <paramref name="killedEntity"/> — reviving and re-killing the same mob won't count again.
    /// Returns the number of requirements updated.
    /// </summary>
    public async Task<int> RecordKill(EntityUid killedEntity, string entityPrototypeId, Guid? playerUserId = null, string? characterName = null)
    {
        var updated = 0;
        var roundId = _gameTicker.RoundId;
        HashSet<int>? credited = null;

        foreach (var goal in _activeGoals)
        {
            foreach (var req in goal.Requirements)
            {
                if (!req.IsKillOrder || req.EntityPrototypeId == null)
                    continue;
                if (!req.EntityPrototypeId.Equals(entityPrototypeId, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only fetch/create the tracking component once we know this death matches
                // at least one kill-order requirement.
                credited ??= EnsureComp<CommunityGoalKillCreditComponent>(killedEntity).CreditedRequirements;
                if (!credited.Add(req.Id))
                    continue; // this exact mob already got credit for this requirement

                await _db.AddCommunityGoalContribution(req.Id, 1, playerUserId, characterName, req.EntityPrototypeId, roundId);
                req.CurrentAmount += 1;
                updated++;

                _sawmill.Debug($"Kill: '{entityPrototypeId}' → goal #{goal.Id} req #{req.Id} " +
                               $"({req.CurrentAmount}/{req.RequiredAmount})");
            }
        }

        if (updated > 0)
            RaiseLocalEvent(new CommunityGoalsUpdatedEvent());

        return updated;
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
                TagId = r.TagId,
                IsKillOrder = r.IsKillOrder,
                DisplayName = r.DisplayName,
                RequiredAmount = r.RequiredAmount,
                CurrentAmount = r.CurrentAmount,
            }).ToList(),
        }).ToList();

        _sawmill.Info($"Loaded {_activeGoals.Count} active community goal(s) for round {roundId}.");
        RaiseLocalEvent(new CommunityGoalsUpdatedEvent());
    }

    /// <summary>
    /// Records a contribution of <paramref name="amount"/> units for every active prototype-based
    /// requirement that matches <paramref name="entityPrototypeId"/> (exact or same stack type).
    /// Tag-based requirements must go through <see cref="RecordContributionByEntity"/> instead.
    /// Returns the number of requirements updated.
    /// </summary>
    public async Task<int> RecordContribution(string entityPrototypeId, long amount, Guid? playerUserId = null, string? characterName = null)
    {
        var itemStackType = GetProtoStackTypeId(entityPrototypeId);
        var updated = 0;
        var roundId = _gameTicker.RoundId;

        foreach (var goal in _activeGoals)
        {
            foreach (var req in goal.Requirements)
            {
                // Tag-based requirements are handled separately via RecordContributionByEntity
                if (req.TagId != null)
                    continue;
                if (req.EntityPrototypeId == null)
                    continue;
                // Kill-order requirements are only satisfied by RecordKill, not by delivering items
                if (req.IsKillOrder)
                    continue;
                if (!MatchesRequirement(entityPrototypeId, itemStackType, req.EntityPrototypeId))
                    continue;

                await _db.AddCommunityGoalContribution(req.Id, amount, playerUserId, characterName, req.EntityPrototypeId, roundId);
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
    /// Records a contribution of <paramref name="amount"/> for every active requirement
    /// (prototype-based OR tag-based) that the given entity satisfies.
    /// Returns the number of requirements updated.
    /// </summary>
    public async Task<int> RecordContributionByEntity(EntityUid item, long amount, Guid? playerUserId = null, string? characterName = null)
    {
        var protoId = MetaData(item).EntityPrototype?.ID;
        var stackType = protoId != null ? GetProtoStackTypeId(protoId) : null;
        var updated = 0;
        var roundId = _gameTicker.RoundId;

        foreach (var goal in _activeGoals)
        {
            foreach (var req in goal.Requirements)
            {
                // Kill-order requirements are only satisfied by RecordKill, not by delivering items
                if (req.IsKillOrder)
                    continue;

                bool matches;
                if (req.TagId != null)
                {
                    matches = _tags.HasTag(item, new ProtoId<TagPrototype>(req.TagId));
                }
                else if (req.EntityPrototypeId != null && protoId != null)
                {
                    matches = MatchesRequirement(protoId, stackType, req.EntityPrototypeId);
                }
                else
                {
                    continue;
                }

                if (!matches)
                    continue;

                var recordProtoId = req.EntityPrototypeId ?? protoId;
                await _db.AddCommunityGoalContribution(req.Id, amount, playerUserId, characterName, recordProtoId, roundId);
                req.CurrentAmount += amount;
                updated++;

                _sawmill.Debug($"Contribution: +{amount} entity={protoId ?? "?"}  → goal #{goal.Id} req #{req.Id} " +
                               $"({req.CurrentAmount}/{req.RequiredAmount})");
            }
        }

        if (updated > 0)
            RaiseLocalEvent(new CommunityGoalsUpdatedEvent());

        return updated;
    }

    /// <summary>
    /// Returns true if an item with <paramref name="itemProtoId"/> (and optional
    /// <paramref name="itemStackTypeId"/>) satisfies a prototype-based requirement defined as
    /// <paramref name="reqProtoId"/>.
    /// For tag-based requirements use <see cref="RecordContributionByEntity"/> instead.
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
    /// Returns true if the given entity satisfies the given requirement data
    /// (handles both prototype-based and tag-based requirements).
    /// </summary>
    public bool EntityMatchesRequirement(EntityUid item, CommunityGoalRequirementData req)
    {
        // Kill-order requirements are only satisfied by killing the entity, not by delivering it
        if (req.IsKillOrder)
            return false;

        if (req.TagId != null)
            return _tags.HasTag(item, new ProtoId<TagPrototype>(req.TagId));

        if (req.EntityPrototypeId == null)
            return false;

        var protoId = MetaData(item).EntityPrototype?.ID;
        if (protoId == null)
            return false;

        var stackType = TryComp<StackComponent>(item, out var sc) ? sc.StackTypeId : null;
        return MatchesRequirement(protoId, stackType, req.EntityPrototypeId);
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
    public async Task RecordContributionToRequirement(int requirementId, long amount, Guid? playerUserId = null, string? characterName = null)
    {
        var roundId = _gameTicker.RoundId;

        // Find the requirement's proto for the contribution record
        string? reqProtoId = null;
        foreach (var goal in _activeGoals)
        {
            foreach (var req in goal.Requirements)
            {
                if (req.Id == requirementId)
                {
                    reqProtoId = req.EntityPrototypeId;
                    break;
                }
            }
            if (reqProtoId != null)
                break;
        }

        await _db.AddCommunityGoalContribution(requirementId, amount, playerUserId, characterName, reqProtoId, roundId);

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
                TagId = r.TagId,
                IsKillOrder = r.IsKillOrder,
                DisplayName = r.DisplayName,
                RequiredAmount = r.RequiredAmount,
                CurrentAmount = r.CurrentAmount,
            }).ToList(),
        }).ToList();

        RaiseLocalEvent(new CommunityGoalsUpdatedEvent());
    }
}
