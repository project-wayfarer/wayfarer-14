using Content.Shared.Containers.ItemSlots;
using Content.Shared.Movement.Systems;
using Content.Shared.PowerCell.Components;

namespace Content.Shared._WF.IPC;

public abstract class SharedPowerCellBodySystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerCellBodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<PowerCellBodyComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<PowerCellBodyComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
    }

    private void OnRefreshSpeed(Entity<PowerCellBodyComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var speed = GetSpeedModifier(ent.Comp);
        args.ModifySpeed(speed, speed);
    }

    private static float GetSpeedModifier(PowerCellBodyComponent comp)
    {
        var best = int.MinValue;
        var speed = 1f;

        foreach (var (level, modifier) in comp.SpeedModifierThresholds)
        {
            if (level > comp.ChargeLevel || level <= best)
                continue;

            best = level;
            speed = modifier;
        }

        return speed;
    }

    private void OnInsertAttempt(Entity<PowerCellBodyComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (IsOwnCellSlot(ent, args.User, args.Slot))
            args.Cancelled = true;
    }

    private void OnEjectAttempt(Entity<PowerCellBodyComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (IsOwnCellSlot(ent, args.User, args.Slot))
            args.Cancelled = true;
    }

    private bool IsOwnCellSlot(Entity<PowerCellBodyComponent> ent, EntityUid? user, ItemSlot slot)
    {
        return ent.Comp.BlockSelfCellAccess
            && user == ent.Owner
            && TryComp<PowerCellSlotComponent>(ent, out var cellSlot)
            && _itemSlots.TryGetSlot(ent, cellSlot.CellSlotId, out var ownSlot)
            && ownSlot == slot;
    }
}
