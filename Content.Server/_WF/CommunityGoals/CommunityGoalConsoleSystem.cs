using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Shared._WF.CommunityGoals.BUI;
using Content.Shared._WF.CommunityGoals.Components;
using Content.Shared._WF.CommunityGoals.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._WF.CommunityGoals;

public sealed class CommunityGoalConsoleSystem : EntitySystem
{
    [Dependency] private readonly CommunityGoalsSystem _goals = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CommunityGoalConsoleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<CommunityGoalConsoleComponent, CommunityGoalContributeMessage>(OnContribute);
    }

    private void OnInit(EntityUid uid, CommunityGoalConsoleComponent comp, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, CommunityGoalConsoleComponent.SlotId, comp.ItemSlot);
    }

    private void OnUIOpened(EntityUid uid, CommunityGoalConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUI(uid, comp);
    }

    private void OnSlotChanged(EntityUid uid, CommunityGoalConsoleComponent comp, ContainerModifiedMessage args)
    {
        UpdateUI(uid, comp);
    }

    private async void OnContribute(EntityUid uid, CommunityGoalConsoleComponent comp, CommunityGoalContributeMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var item = comp.ItemSlot.Item;
        if (item == null)
        {
            _audio.PlayPvs(comp.ErrorSound, uid);
            _popup.PopupEntity(Loc.GetString("community-goal-console-no-item"), uid, player);
            return;
        }

        var protoId = MetaData(item.Value).EntityPrototype?.ID;
        if (protoId == null)
        {
            _audio.PlayPvs(comp.ErrorSound, uid);
            _popup.PopupEntity(Loc.GetString("community-goal-console-unknown-item"), uid, player);
            return;
        }

        // Determine contribution amount (stack or 1)
        long amount = 1;
        if (TryComp<StackComponent>(item.Value, out var stack))
            amount = stack.Count;

        var itemName = Name(item.Value);

        // Check that at least one active requirement matches before consuming
        var matched = _goals.ActiveGoals
            .Any(g => g.Requirements.Any(r =>
                r.EntityPrototypeId.Equals(protoId, StringComparison.OrdinalIgnoreCase)));

        if (!matched)
        {
            _audio.PlayPvs(comp.ErrorSound, uid);
            _popup.PopupEntity(Loc.GetString("community-goal-console-not-needed", ("item", itemName)), uid, player);
            return;
        }

        // Consume the item
        _itemSlots.TryEject(uid, comp.ItemSlot, null, out _);
        QueueDel(item.Value);

        // Record contribution
        var updated = await _goals.RecordContribution(protoId, amount);

        _audio.PlayPvs(comp.ContributeSound, uid);
        _popup.PopupEntity(
            Loc.GetString("community-goal-console-contributed", ("amount", amount), ("item", itemName)),
            uid, player);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(player)} contributed {amount}x {protoId} to {updated} community goal requirement(s).");

        UpdateUI(uid, comp);
    }

    private void UpdateUI(EntityUid uid, CommunityGoalConsoleComponent comp)
    {
        string? slotProto = null;
        long slotAmount = 0;
        string? slotName = null;

        var item = comp.ItemSlot.Item;
        if (item != null)
        {
            slotProto = MetaData(item.Value).EntityPrototype?.ID;
            slotName = Name(item.Value);
            slotAmount = TryComp<StackComponent>(item.Value, out var stack) ? stack.Count : 1;
        }

        var state = new CommunityGoalConsoleState(
            _goals.ActiveGoals.ToList(),
            slotProto,
            slotAmount,
            slotName);

        _uiSystem.SetUiState(uid, CommunityGoalConsoleUiKey.Key, state);
    }
}
