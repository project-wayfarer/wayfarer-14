using Content.Server.Cargo.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.CCVar;
using Content.Shared.Mind.Components;
using Content.Shared.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Reflection;

namespace Content.Server._Mono.Cleanup;

/// <summary>
///     Deletes entities eligible for deletion.
/// </summary>
public sealed partial class SpaceCleanupSystem : BaseCleanupSystem<PhysicsComponent>
{
    [Dependency] private CleanupHelperSystem _cleanup = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    private object _manifold = default!;
    private MethodInfo _testOverlap = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private float _maxDistance;
    private float _maxGridDistance;
    private float _maxPrice;

    private EntityQuery<CleanupImmuneComponent> _immuneQuery;
    private EntityQuery<FixturesComponent> _fixQuery;
    private EntityQuery<HTNComponent> _htnQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MindContainerComponent> _mindQuery;
    private EntityQuery<PhysicsComponent> _physQuery;

    private List<(EntityCoordinates Coord, TimeSpan Time, float Radius, float Aggression)> _sweepQueue = new();
    private HashSet<Entity<PhysicsComponent>> _sweepEnts = new();

    public override void Initialize()
    {
        base.Initialize();

        // this queries over literally everything with PhysicsComponent so has to have big interval
        _cleanupInterval = TimeSpan.FromSeconds(900); // Wayfarer: 600<900

        _immuneQuery = GetEntityQuery<CleanupImmuneComponent>();
        _fixQuery = GetEntityQuery<FixturesComponent>();
        _htnQuery = GetEntityQuery<HTNComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _mindQuery = GetEntityQuery<MindContainerComponent>();
        _physQuery = GetEntityQuery<PhysicsComponent>();

        Subs.CVar(_cfg, MonoCVars.CleanupMaxGridDistance, val => _maxGridDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.SpaceCleanupDistance, val => _maxDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.SpaceCleanupMaxValue, val => _maxPrice = val, true);

        var manifoldType = typeof(SharedMapSystem).Assembly.GetType("Robust.Shared.Physics.Collision.IManifoldManager");
        if (manifoldType != null)
        {
            _manifold = IoCManager.ResolveType(manifoldType);
            var testOverlapMethod = manifoldType.GetMethod("TestOverlap");
            if (testOverlapMethod != null)
                _testOverlap = testOverlapMethod.MakeGenericMethod(typeof(IPhysShape), typeof(PhysShapeCircle));
        }
    }

    protected override bool ShouldEntityCleanup(EntityUid uid)
    {
        return ShouldEntityCleanup(uid, 1f);
    }

    private bool ShouldEntityCleanup(EntityUid uid, float aggression)
    {
        var xform = Transform(uid);
        /* // Wayfarer: Old logic. Has an edge case and I think it could be a little better, so... Let's order it.
        var isStuck = false;

        var price = 0f;

        return !_gridQuery.HasComp(uid)
            && (xform.ParentUid == xform.MapUid // don't delete if on grid
                || (isStuck |= GetWallStuck((uid, xform)))) // or wall-stuck
            && !_htnQuery.HasComp(uid) // handled by MobCleanupSystem
            && !_immuneQuery.HasComp(uid) // handled by GridCleanupSystem
            && !_mindQuery.HasComp(uid) // no deleting anything that can have a mind - should be handled by MobCleanupSystem anyway
            && (price = (float)_pricing.GetPrice(uid)) <= _maxPrice
            && (isStuck
                || !_cleanup.HasNearbyGrids(xform.Coordinates, _maxGridDistance * aggression * MathF.Sqrt(price / _maxPrice))
                    && !_cleanup.HasNearbyPlayers(xform.Coordinates, _maxDistance * aggression * MathF.Sqrt(price / _maxPrice)));
        */

        // Wayfarer Start
        // Experimental, but I avoided an edge case here and also tried ordering things based on how I expensive I -THINK- certain checks are. No data backs this up.
        // 1. Component lookups - these are probably faster, I think. So we should probably check them first, to deny it faster.
        if (_gridQuery.HasComp(uid)      // never delete if on grid.
            || _htnQuery.HasComp(uid)    // handled by MobCleanupSystem.
            || _immuneQuery.HasComp(uid) // handled by GridCleanupSystem.
            || _mindQuery.HasComp(uid))  // anything that could have a mind.
        {
            return false;
        }

        // 2. Location eligibility – entity must be on a map OR wall‑stuck
        bool isStuck = false;
        bool eligibleLocation;
        if (xform.ParentUid == xform.MapUid)
        {
            eligibleLocation = true;
        }
        else
        {
            // GetWallStuck seems like a performance intensive method...
            isStuck = GetWallStuck((uid, xform));
            eligibleLocation = isStuck;
        }

        if (!eligibleLocation)
            return false;

        // 3. Price check
        var price = (float)_pricing.GetPrice(uid);
        if (price > _maxPrice && _maxPrice > 0f) // if no price limit, everything passes
            return false;

        // 4. If stuck, we don’t care about nearby players/grids – clean it up. Physics lag bad.
        if (isStuck)
            return true;

        // 5. Handle edge cases where radius would be less or equal 0, we don't want to divide by zero now, do we?
        // If maxPrice is 0 (no limit) or price is 0, use full radius, as I don't know if certain really important things have 0 price.
        if (_maxPrice <= 0f || price <= 0f)
        {
            bool hasPlayers = _cleanup.HasNearbyPlayers(xform.Coordinates, _maxDistance * aggression);
            bool hasGrids = _cleanup.HasNearbyGrids(xform.Coordinates, _maxGridDistance * aggression);
            return !hasGrids && !hasPlayers;
        }

        // 6. Calculate scaled radii (EXPENSIVE)
        var scale = MathF.Sqrt(price / _maxPrice);
        var playerRadius = _maxDistance * aggression * scale;
        var gridRadius = _maxGridDistance * aggression * scale;

        // 7. Proximity checks (VERY EXPENSIVE CHECK, GOES LAST.)
        bool hasNearbyPlayers = playerRadius > 0f && _cleanup.HasNearbyPlayers(xform.Coordinates, playerRadius);
        bool hasNearbyGrids = gridRadius > 0f && _cleanup.HasNearbyGrids(xform.Coordinates, gridRadius);
        return !hasNearbyGrids && !hasNearbyPlayers;
        // Wayfarer End
    }

    private bool GetWallStuck(Entity<TransformComponent> ent)
    {
        if (ent.Comp.GridUid is not { } gridUid
            || ent.Comp.Anchored
            || ent.Comp.ParentUid != gridUid // ignore if not directly parented to grid
        )
            return false;

        var xfB = new Transform(ent.Comp.LocalPosition, 0);
        var shapeB = new PhysShapeCircle(0.001f);

        var contacts = _physics.GetContacts(ent.Owner);
        // it dies without this for some reason
        if (contacts == ContactEnumerator.Empty)
            return false;

        while (contacts.MoveNext(out var contact))
        {
            if (contact.FixtureA == null
                || contact.FixtureB == null
                || contact.BodyA == null
                || contact.BodyB == null
                || !contact.FixtureA.Hard
                || !contact.FixtureB.Hard
                || !contact.IsTouching
            )
                continue;

            var isA = contact.EntityB == ent.Owner;

            var body = isA ? contact.BodyA : contact.BodyB;
            // only trigger when the other entity is static
            if ((body.BodyType & BodyType.Static) == 0)
                continue;

            var fix = isA ? contact.FixtureA : contact.FixtureB;
            var xform = isA ? contact.XformA : contact.XformB;
            var anch = isA ? contact.EntityA : contact.EntityB;

            var xf = _physics.GetLocalPhysicsTransform(anch, xform);
            var shape = fix.Shape;

            if ((bool?)_testOverlap.Invoke(_manifold, [shape, 0, shapeB, 0, xf, xfB]) ?? false)
                return true;
        }

        return false;
    }

    public void QueueSweep(EntityCoordinates coordinates, TimeSpan time, float radius, float aggression)
    {
        _sweepQueue.Add((coordinates, time, radius, aggression));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (int i = _sweepQueue.Count - 1; i >= 0; i--)
        {
            var (coord, time, radius, aggression) = _sweepQueue[i];

            if (_timing.CurTime < time)
                continue;

            _sweepQueue.RemoveAt(i);
            if (!coord.IsValid(EntityManager))
                continue;

            _sweepEnts.Clear();
            _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(coord), radius, _sweepEnts, LookupFlags.Dynamic | LookupFlags.Approximate | LookupFlags.Sundries);

            foreach (var (uid, body) in _sweepEnts)
            {
                if (ShouldEntityCleanup(uid, aggression))
                    CleanupEnt(uid);
            }
        }
    }
}
