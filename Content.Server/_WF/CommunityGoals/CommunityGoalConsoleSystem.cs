using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Server.Research.Disk;
using Content.Shared._WF.CommunityGoals;
using Content.Shared._WF.CommunityGoals.BUI;
using Content.Shared._WF.CommunityGoals.Components;
using Content.Shared._WF.CommunityGoals.Events;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._WF.CommunityGoals;

public sealed class CommunityGoalConsoleSystem : EntitySystem
{
    [Dependency] private readonly CommunityGoalsSystem _goals = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CommunityGoalConsoleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, CommunityGoalCommitMessage>(OnCommit);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, CommunityGoalClearStagingMessage>(OnClearStaging);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, CommunityGoalContributeToRequirementMessage>(OnContributeToRequirement);
        SubscribeLocalEvent<CommunityGoalsUpdatedEvent>(OnGoalsUpdated);
    }

    private void OnInit(EntityUid uid, CommunityGoalConsoleComponent comp, ComponentInit args)
    {
        _containers.EnsureContainer<Container>(uid, CommunityGoalConsoleComponent.StagingContainerId);
    }

    /// <summary>
    /// Whenever the active goals list changes (contribution, admin edit, round start),
    /// push fresh state to every community goal console that has open UIs.
    /// </summary>
    private void OnGoalsUpdated(CommunityGoalsUpdatedEvent ev)
    {
        var query = EntityQueryEnumerator<CommunityGoalConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_uiSystem.IsUiOpen(uid, CommunityGoalConsoleUiKey.Key))
                UpdateUI(uid, comp);
        }
    }

    private void OnUIOpened(EntityUid uid, CommunityGoalConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUI(uid, comp);
    }

    private void OnContainerChanged(EntityUid uid, CommunityGoalConsoleComponent comp, ContainerModifiedMessage args)
    {
        if (args.Container.ID != CommunityGoalConsoleComponent.StagingContainerId)
            return;
        UpdateUI(uid, comp);
    }

    /// <summary>
    /// When a player uses an item on the console, stage it for contribution.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, CommunityGoalConsoleComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_containers.TryGetContainer(uid, CommunityGoalConsoleComponent.StagingContainerId, out var container))
            return;

        var item = args.Used;
        var protoId = MetaData(item).EntityPrototype?.ID;

        if (protoId == null)
        {
            _popup.PopupEntity(Loc.GetString("community-goal-console-unknown-item"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Match by exact proto OR shared stack type (e.g. SheetSteel10 matches a SheetSteel requirement)
        var itemStackType = TryComp<StackComponent>(item, out var sc) ? sc.StackTypeId : null;
        var matched = _goals.ActiveGoals
            .Any(g => g.Requirements.Any(r =>
                _goals.MatchesRequirement(protoId, itemStackType, r.EntityPrototypeId)));

        if (!matched)
        {
            _popup.PopupEntity(
                Loc.GetString("community-goal-console-not-needed", ("item", Name(item))),
                uid, args.User);
            args.Handled = true;
            return;
        }

        if (container.ContainedEntities.Count >= comp.MaxStagingItems)
        {
            _popup.PopupEntity(Loc.GetString("community-goal-console-staging-full"), uid, args.User);
            args.Handled = true;
            return;
        }

        if (!_containers.Insert(item, container))
        {
            args.Handled = true;
            return;
        }

        long amount = GetItemAmount(item);
        _audio.PlayPvs(comp.InsertSound, uid);
        _popup.PopupEntity(
            Loc.GetString("community-goal-console-item-staged", ("amount", amount), ("item", Name(item))),
            uid, args.User);

        args.Handled = true;
    }

    /// <summary>
    /// Commits all staged items: records contributions in the DB and deletes the items.
    /// </summary>
    private async void OnCommit(EntityUid uid, CommunityGoalConsoleComponent comp, CommunityGoalCommitMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        if (!_containers.TryGetContainer(uid, CommunityGoalConsoleComponent.StagingContainerId, out var container))
            return;

        if (container.ContainedEntities.Count == 0)
        {
            _audio.PlayPvs(comp.ErrorSound, uid);
            return;
        }

        // Aggregate contributions, normalizing each item's proto to the matching requirement's proto.
        // e.g. SheetSteel10 → records as SheetSteel (whatever the requirement is defined as).
        var contributions = new Dictionary<string, long>();
        var names = new Dictionary<string, string>();

        foreach (var ent in container.ContainedEntities)
        {
            var protoId = MetaData(ent).EntityPrototype?.ID;
            if (protoId == null)
                continue;

            long amount = GetItemAmount(ent);
            var itemStackType = TryComp<StackComponent>(ent, out var stackComp) ? stackComp.StackTypeId : null;

            // Find the requirement proto this item maps to (for canonical recording).
            var reqProtoId = _goals.ActiveGoals
                .SelectMany(g => g.Requirements)
                .FirstOrDefault(r => _goals.MatchesRequirement(protoId, itemStackType, r.EntityPrototypeId))
                ?.EntityPrototypeId ?? protoId;

            if (contributions.TryGetValue(reqProtoId, out var existing))
                contributions[reqProtoId] = existing + amount;
            else
                contributions[reqProtoId] = amount;

            names[reqProtoId] = Name(ent);
        }

        // Record each unique prototype contribution in the DB first, then delete.
        // This order ensures items are not lost if the DB write fails.
        var totalUpdated = 0;
        try
        {
            foreach (var (protoId, amount) in contributions)
            {
                var updated = await _goals.RecordContribution(protoId, amount);
                totalUpdated += updated;

                if (updated > 0)
                {
                    _adminLog.Add(LogType.Action, LogImpact.Low,
                        $"{ToPrettyString(player)} contributed {amount}x {protoId} to {updated} community goal requirement(s).");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to record community goal contribution for {ToPrettyString(player)}: {ex}");
            _audio.PlayPvs(comp.ErrorSound, uid);
            _popup.PopupEntity(Loc.GetString("community-goal-console-commit-failed"), uid, player);
            UpdateUI(uid, comp);
            return;
        }

        // Only delete items after a successful DB write.
        foreach (var ent in container.ContainedEntities.ToList())
            QueueDel(ent);

        _audio.PlayPvs(comp.CommitSound, uid);
        _popup.PopupEntity(
            Loc.GetString("community-goal-console-committed", ("types", contributions.Count)),
            uid, player);

        UpdateUI(uid, comp);
    }

    /// <summary>
    /// Contributes all staged items that match a specific requirement, leaving others in place.
    /// </summary>
    private async void OnContributeToRequirement(EntityUid uid, CommunityGoalConsoleComponent comp, CommunityGoalContributeToRequirementMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        if (!_containers.TryGetContainer(uid, CommunityGoalConsoleComponent.StagingContainerId, out var container))
            return;

        // Locate the target requirement in the active goal cache.
        CommunityGoalRequirementData? targetReq = null;
        foreach (var goal in _goals.ActiveGoals)
        {
            foreach (var req in goal.Requirements)
            {
                if (req.Id == args.RequirementId)
                {
                    targetReq = req;
                    break;
                }
            }
            if (targetReq != null)
                break;
        }

        if (targetReq == null)
        {
            _audio.PlayPvs(comp.ErrorSound, uid);
            return;
        }

        // Collect staged items that match this requirement.
        var toConsume = new List<EntityUid>();
        long totalAmount = 0;
        var itemName = targetReq.DisplayName ?? targetReq.EntityPrototypeId;

        foreach (var ent in container.ContainedEntities)
        {
            var protoId = MetaData(ent).EntityPrototype?.ID;
            if (protoId == null)
                continue;

            var itemStackType = TryComp<StackComponent>(ent, out var sc) ? sc.StackTypeId : null;
            if (!_goals.MatchesRequirement(protoId, itemStackType, targetReq.EntityPrototypeId))
                continue;

            long amount = GetItemAmount(ent);
            toConsume.Add(ent);
            totalAmount += amount;
            itemName = Name(ent);
        }

        if (toConsume.Count == 0)
        {
            _audio.PlayPvs(comp.ErrorSound, uid);
            return;
        }

        // Record contribution first, then delete — so items are not lost if the DB write fails.
        try
        {
            await _goals.RecordContributionToRequirement(targetReq.Id, totalAmount);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to record targeted community goal contribution for {ToPrettyString(player)}: {ex}");
            _audio.PlayPvs(comp.ErrorSound, uid);
            _popup.PopupEntity(Loc.GetString("community-goal-console-commit-failed"), uid, player);
            UpdateUI(uid, comp);
            return;
        }

        // Only delete items after a successful DB write.
        foreach (var ent in toConsume)
            QueueDel(ent);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(player)} contributed {totalAmount}x {itemName} to community goal requirement #{targetReq.Id}.");

        _audio.PlayPvs(comp.CommitSound, uid);
        _popup.PopupEntity(
            Loc.GetString("community-goal-console-contributed-targeted",
                ("amount", totalAmount),
                ("item", itemName)),
            uid, player);

        UpdateUI(uid, comp);
    }

    /// <summary>
    /// Returns the contribution amount for a staged entity.
    /// Research disks contribute their <c>Points</c> value;
    /// stacks contribute their count; everything else contributes 1.
    /// </summary>
    private long GetItemAmount(EntityUid ent)
    {
        if (TryComp<ResearchDiskComponent>(ent, out var disk))
            return disk.Points;
        if (TryComp<StackComponent>(ent, out var stack))
            return stack.Count;
        return 1;
    }

    /// <summary>
    /// Ejects all staged items back to the floor around the console.
    /// </summary>
    private void OnClearStaging(EntityUid uid, CommunityGoalConsoleComponent comp, CommunityGoalClearStagingMessage args)
    {
        if (!_containers.TryGetContainer(uid, CommunityGoalConsoleComponent.StagingContainerId, out var container))
            return;

        _containers.EmptyContainer(container);
        UpdateUI(uid, comp);
    }

    private void UpdateUI(EntityUid uid, CommunityGoalConsoleComponent comp)
    {
        var staged = new List<StagedItemData>();

        if (_containers.TryGetContainer(uid, CommunityGoalConsoleComponent.StagingContainerId, out var container))
        {
            // Group staged items by their matched requirement proto ID for consistent display.
            var groups = new Dictionary<string, (long amount, string name)>();

            foreach (var ent in container.ContainedEntities)
            {
                var protoId = MetaData(ent).EntityPrototype?.ID;
                if (protoId == null)
                    continue;

                long amount = GetItemAmount(ent);
                var itemStackType = TryComp<StackComponent>(ent, out var stackComp) ? stackComp.StackTypeId : null;
                var display = Name(ent);

                // Normalize to requirement proto so variants (SheetSteel10 etc.) merge correctly.
                var groupKey = _goals.ActiveGoals
                    .SelectMany(g => g.Requirements)
                    .FirstOrDefault(r => _goals.MatchesRequirement(protoId, itemStackType, r.EntityPrototypeId))
                    ?.EntityPrototypeId ?? protoId;

                if (groups.TryGetValue(groupKey, out var existing))
                    groups[groupKey] = (existing.amount + amount, display);
                else
                    groups[groupKey] = (amount, display);
            }

            foreach (var (protoId, (amount, display)) in groups)
                staged.Add(new StagedItemData(protoId, display, amount));
        }

        var state = new CommunityGoalConsoleState(_goals.ActiveGoals.ToList(), staged);
        _uiSystem.SetUiState(uid, CommunityGoalConsoleUiKey.Key, state);
    }
}
