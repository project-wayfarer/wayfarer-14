using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Server.Effects;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Physics; // Wayfarer
using Robust.Shared.Physics.Components; // Wayfarer
using Robust.Shared.Physics.Dynamics; // Wayfarer
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems; // Wayfarer
using Robust.Shared.Player;
using Robust.Shared.Timing; // Wayfarer
using System.Linq; // Wayfarer

namespace Content.Server.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ColorFlashEffectSystem _color = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    // Wayfarer - Raytraced projectiles (Adapted from Mono)
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private EntityQuery<PhysicsComponent> _physQuery;
    private EntityQuery<FixturesComponent> _fixQuery;

    /// <summary>
    /// Minimum velocity for a projectile to be considered for raycast hit detection.
    /// Projectiles slower than this will rely on standard StartCollideEvent.
    /// </summary>
    private const float MinRaycastVelocity = 75f;
    // End Wayfarer

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);

        // Wayfarer - Raytraced projectiles
        _physQuery = GetEntityQuery<PhysicsComponent>();
        _fixQuery = GetEntityQuery<FixturesComponent>();
        // End Wayfarer
    }

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        // This is so entities that shouldn't get a collision are ignored.
        if (args.OurFixtureId != ProjectileFixture || !args.OtherFixture.Hard
            || component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        var target = args.OtherEntity;
        // it's here so this check is only done once before possible hit
        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            SetShooter(uid, component, target);
            return;
        }

        var ev = new ProjectileHitEvent(component.Damage * _damageableSystem.UniversalProjectileDamageModifier, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);

        var otherName = ToPrettyString(target);
        var damageRequired = _destructibleSystem.DestroyedAt(target);
        if (TryComp<DamageableComponent>(target, out var damageableComponent))
        {
            damageRequired -= damageableComponent.TotalDamage;
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }
        var modifiedDamage = _damageableSystem.TryChangeDamage(target, ev.Damage, component.IgnoreResistances, damageable: damageableComponent, origin: component.Shooter);
        var deleted = Deleted(target);

        if (modifiedDamage is not null && Exists(component.Shooter))
        {
            if (modifiedDamage.AnyPositive() && !deleted)
            {
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));
            }

            _adminLogger.Add(LogType.BulletHit,
                LogImpact.Medium,
                $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter!.Value):user} hit {otherName:target} and dealt {modifiedDamage.GetTotal():damage} damage");
        }

        // If penetration is to be considered, we need to do some checks to see if the projectile should stop.
        if (modifiedDamage is not null && component.PenetrationThreshold != 0)
        {
            // If a damage type is required, stop the bullet if the hit entity doesn't have that type.
            if (component.PenetrationDamageTypeRequirement != null)
            {
                var stopPenetration = false;
                foreach (var requiredDamageType in component.PenetrationDamageTypeRequirement)
                {
                    if (!modifiedDamage.DamageDict.Keys.Contains(requiredDamageType))
                    {
                        stopPenetration = true;
                        break;
                    }
                }
                if (stopPenetration)
                    component.ProjectileSpent = true;
            }

            // If the object won't be destroyed, it "tanks" the penetration hit.
            if (modifiedDamage.GetTotal() < damageRequired)
            {
                component.ProjectileSpent = true;
            }

            if (!component.ProjectileSpent)
            {
                component.PenetrationAmount += damageRequired;
                // The projectile has dealt enough damage to be spent.
                if (component.PenetrationAmount >= component.PenetrationThreshold)
                {
                    component.ProjectileSpent = true;
                }
            }
        }
        else
        {
            component.ProjectileSpent = true;
        }

        if (!deleted)
        {
            _guns.PlayImpactSound(target, modifiedDamage, component.SoundHit, component.ForceSound);

            if (!args.OurBody.LinearVelocity.IsLengthZero())
                _sharedCameraRecoil.KickCamera(target, args.OurBody.LinearVelocity.Normalized());
        }

        if (component.DeleteOnCollide && component.ProjectileSpent)
            QueueDel(uid);

        if (component.ImpactEffect != null && TryComp(uid, out TransformComponent? xform))
        {
            RaiseNetworkEvent(new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates)), Filter.Pvs(xform.Coordinates, entityMan: EntityManager));
        }
    }

    // Wayfarer - Raytraced Projectiles
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var projectileComp, out var physicsComp, out var xform))
        {
            if (projectileComp.ProjectileSpent)
                continue;

            var currentVelocity = physicsComp.LinearVelocity;
            if (currentVelocity.Length() < MinRaycastVelocity)
                continue;

            var lastPosition = _transformSystem.GetWorldPosition(xform);
            var rayDirection = currentVelocity.Normalized();
            // Ensure rayDistance is not zero to prevent issues with IntersectRay if frametime or velocity is zero.
            var rayDistance = currentVelocity.Length() * frameTime;
            if (rayDistance <= 0f)
                continue;

            if (!_fixQuery.TryComp(uid, out var fix) || !fix.Fixtures.TryGetValue(ProjectileFixture, out var projFix))
                return;

            var hits = _physics.IntersectRay(xform.MapID,
                new CollisionRay(lastPosition, rayDirection, projFix.CollisionMask),
                rayDistance,
                uid, // Entity to ignore (self)
                false) // IncludeNonHard = false
                .ToList();

            hits.RemoveAll(hit => {
                var hitEnt = hit.HitEntity;

                if (!_physQuery.TryComp(hitEnt, out var otherBody) || !_fixQuery.TryComp(hitEnt, out var otherFix))
                    return true;

                Fixture? hitFix = null;
                foreach (var kv in otherFix.Fixtures)
                {
                    if (kv.Value.Hard)
                    {
                        hitFix = kv.Value;
                        break;
                    }
                }
                if (hitFix == null)
                    return true;

                // this is cursed but necessary
                var ourEv = new PreventCollideEvent(uid, hitEnt, physicsComp, otherBody, projFix, hitFix);
                RaiseLocalEvent(uid, ref ourEv);
                if (ourEv.Cancelled)
                    return true;

                var otherEv = new PreventCollideEvent(hitEnt, uid, otherBody, physicsComp, hitFix, projFix);
                RaiseLocalEvent(hitEnt, ref otherEv);
                return otherEv.Cancelled;
            });

            if (hits.Count > 0)
            {
                // Process the closest hit
                // IntersectRay results are not guaranteed to be sorted by distance, so we sort them.
                hits.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                var closestHit = hits.First();

                // teleport us so we hit it
                // this is cursed but i don't think there's a better way to force a collision here
                _transformSystem.SetWorldPosition(uid, _transformSystem.GetWorldPosition(closestHit.HitEntity));
                continue;
            }
        }
    }
    // End Wayfarer
}
