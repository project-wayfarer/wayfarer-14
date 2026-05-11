using Content.Shared._WF.RCD.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Robust.Shared.Timing;

// Wayfarer: Moved shipyard RCD ammo logic

namespace Content.Shared._WF.RCD.Systems;

public sealed class ShipyardRCDAmmoSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipyardRCDAmmoComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, ShipyardRCDAmmoComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_timing.IsFirstTimePredicted)
            return;

        if (args.Target is not { Valid: true } target)
            return;

        if (!HasComp<RCDComponent>(target))
            return;

        var shipyardRcd = HasComp<ShipyardRCDComponent>(target);

        if (shipyardRcd != comp.IsShipyardRCDAmmo)
        {
            _popup.PopupClient(
                Loc.GetString("rcd-component-wrong-ammo-type"),
                target,
                args.User);

            args.Handled = true;
        }
    }
}
