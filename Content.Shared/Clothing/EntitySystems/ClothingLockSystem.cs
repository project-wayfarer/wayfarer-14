using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Utility;

namespace Content.Shared.Clothing.EntitySystems;

/// <summary>
/// System that prevents clothing from being removed when a ClothingLock item is worn.
/// Can be configured to lock specific slots or all slots.
/// This is intended for use with collar modules to create a clothing lock.
/// </summary>
public sealed class ClothingLockSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingLockComponent, ExaminedEvent>(OnExamined);
        // Listen on the ClothingLock item itself - the inventory relay system will forward unequip attempts
        SubscribeLocalEvent<ClothingLockComponent, InventoryRelayedEvent<IsUnequippingTargetAttemptEvent>>(OnUnequipAttempt);
    }

    private void OnExamined(Entity<ClothingLockComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("clothing-lock-examine"));
    }

    private void OnUnequipAttempt(Entity<ClothingLockComponent> ent, ref InventoryRelayedEvent<IsUnequippingTargetAttemptEvent> args)
    {
        // Allow the collar itself to be removed, but prevent other clothing removal based on configuration
        if (args.Args.Equipment == ent.Owner)
            return;

        // WF support Psychic clothing lock
        TryComp<TransformComponent>(ent.Owner, out var comp);
        if (comp == null)
        {
            return;
        }

        // If being removed by someone else, and not configured to block others, short circuit
        if (args.Args.Unequipee != comp.ParentUid && ent.Comp.BlockOthers == false)
        {
            return;
        }

        // If LockedSlots is null or empty, lock all clothing
        if (ent.Comp.LockedSlots == null || ent.Comp.LockedSlots.Count == 0)
        {
            args.Args.Reason = ent.Comp.BlockOthers == false ? "clothing-lock-prevent-removal-self" : "clothing-lock-prevent-removal"; // WF psychic collar lock
            args.Args.Cancel();
            return;
        }

        // Only lock specific slots if configured
        if (_inventorySystem.TryGetContainingSlot((args.Args.Equipment, null, null), out var slotDef))
        {
            if (ent.Comp.LockedSlots.Contains(slotDef.Name))
            {
                args.Args.Reason = ent.Comp.BlockOthers == false ? "clothing-lock-prevent-removal-self" : "clothing-lock-prevent-removal"; // WF psychic collar lock
                args.Args.Cancel();
            }
        }
    }
}
