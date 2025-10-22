using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed class SizeManipulatorSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SizeManipulatorComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, SizeManipulatorComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        ToggleMode(uid, component, args.User);
        args.Handled = true;
    }

    public void ToggleMode(EntityUid uid, SizeManipulatorComponent component, EntityUid? user = null)
    {
        component.Mode = component.Mode == SizeManipulatorMode.Grow
            ? SizeManipulatorMode.Shrink
            : SizeManipulatorMode.Grow;

        Dirty(uid, component);

        // Update the projectile prototype on the battery ammo provider
        if (TryComp<ProjectileBatteryAmmoProviderComponent>(uid, out var projectileProvider))
        {
            projectileProvider.Prototype = component.Mode == SizeManipulatorMode.Grow
                ? component.GrowPrototype
                : component.ShrinkPrototype;
            Dirty(uid, projectileProvider);
        }

        var message = component.Mode == SizeManipulatorMode.Grow
            ? Loc.GetString("size-manipulator-mode-grow")
            : Loc.GetString("size-manipulator-mode-shrink");

        if (user != null && _net.IsClient)
            _popup.PopupClient(message, uid, user.Value);
        else if (user != null)
            _popup.PopupEntity(message, uid, user.Value);
    }
}
