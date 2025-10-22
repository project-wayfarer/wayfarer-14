using Content.Server.Body.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Log;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed class SizeManipulatorSystem : EntitySystem
{
    [Dependency] private readonly SizeManipulationSystem _sizeManipulation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BulletSizeManipulatorComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(EntityUid uid, BulletSizeManipulatorComponent component, ref ProjectileHitEvent args)
    {
        var hitEntity = args.Target;

        if (!Exists(hitEntity))
        {
            Logger.Debug("SizeManipulator: Hit entity doesn't exist");
            return;
        }

        Logger.Debug($"SizeManipulator: Projectile {ToPrettyString(uid)} hit entity {ToPrettyString(hitEntity)}, applying size change mode: {component.Mode}");

        // Apply size change to the hit entity
        _sizeManipulation.TryChangeSize(hitEntity, component.Mode, args.Shooter);
    }
}
