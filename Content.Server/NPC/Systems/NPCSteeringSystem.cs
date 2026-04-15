using System.Buffers;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.DoAfter;
using Content.Server.Gravity;
using Content.Server.NPC.Components;
using Content.Server.NPC.Events;
using Content.Server.NPC.Pathfinding;
using Content.Shared.CCVar;
using Content.Shared.Climbing.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Events;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Enums;
using Content.Shared.Prying.Systems;
using Microsoft.Extensions.ObjectPool;
using Prometheus;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCSteeringSystem : SharedNPCSteeringSystem
{
    private static readonly Gauge ActiveSteeringGauge = Metrics.CreateGauge(
        "npc_steering_active_count",
        "Amount of NPCs trying to actively do steering");

    /*
     * We use context steering to determine which way to move.
     * This involves creating an array of possible directions and assigning a value for the desireability of each direction.
     *
     * There's multiple ways to implement this, e.g. you can average all directions, or you can choose the highest direction
     * , or you can remove the danger map entirely and only having an interest map (AKA game endeavour).
     * See http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter18_Context_Steering_Behavior-Driven_Steering_at_the_Macro_Scale.pdf
     * (though in their case it was for an F1 game so used context steering across the width of the road).
     */

    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GravitySystem _gravity = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly PathfindingSystem _pathfindingSystem = default!;
    [Dependency] private readonly PryingSystem _pryingSystem = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;

    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<MovementSpeedModifierComponent> _modifierQuery;
    private EntityQuery<NPCMeleeCombatComponent> _npcMeleeQuery;
    private EntityQuery<NPCRangedCombatComponent> _npcRangedQuery;
    private EntityQuery<NpcFactionMemberComponent> _factionQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private ObjectPool<HashSet<EntityUid>> _entSetPool =
        new DefaultObjectPool<HashSet<EntityUid>>(new SetPolicy<EntityUid>());

    /// <summary>
    /// Enabled antistuck detection so if an NPC is in the same spot for a while it will re-path.
    /// </summary>
    public bool AntiStuck = true;

    private bool _enabled;

    private bool _pathfinding = true;
    private bool _pathfindingCombatOnly;
    private bool _pathShareEnabled;
    private float _pathShareRadius;
    private float _pathShareActivationRange;
    private float _pathShareTargetTolerance;
    private float _pathShareBreakawayChance;
    private float _pathShareBreakawayDuration;
    private float _pathShareDirectOverrideRatio;
    private bool _pathShareNonCombatEnabled;
    private bool _pathShareNonCombatDynamic;
    private int _pathShareNonCombatMaxSkip;
    private float _pathShareNonCombatFlipChance;
    private float _pathShareLoopFlipEndpointTolerance;

    private static readonly TimeSpan SharedPathLifetime = TimeSpan.FromSeconds(1.5);
    private readonly Dictionary<PathGroupKey, SharedPathSnapshot> _sharedPaths = new();
    private readonly List<PathGroupKey> _prunePathGroups = new();
    private readonly Dictionary<EntityUid, TimeSpan> _breakawayUntil = new();
    private readonly List<EntityUid> _pruneBreakaway = new();
    private TimeSpan _nextSharedPathPrune;

    private readonly record struct PathGroupKey(EntityUid TargetUid, MapId MapId, PathFlags Flags);

    private sealed class SharedPathSnapshot
    {
        public MapCoordinates Origin;
        public MapCoordinates Target;
        public List<PathPoly> Path = new();
        public TimeSpan Timestamp;
    }

    public static readonly Vector2[] Directions = new Vector2[InterestDirections];

    private readonly HashSet<ICommonSession> _subscribedSessions = new();

    private object _obstacles = new();

    private int _activeSteeringCount;

    public override void Initialize()
    {
        base.Initialize();

        Log.Level = LogLevel.Info;
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _modifierQuery = GetEntityQuery<MovementSpeedModifierComponent>();
        _npcMeleeQuery = GetEntityQuery<NPCMeleeCombatComponent>();
        _npcRangedQuery = GetEntityQuery<NPCRangedCombatComponent>();
        _factionQuery = GetEntityQuery<NpcFactionMemberComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        for (var i = 0; i < InterestDirections; i++)
        {
            Directions[i] = new Angle(InterestRadians * i).ToVec();
        }

        UpdatesBefore.Add(typeof(SharedPhysicsSystem));
        Subs.CVar(_configManager, CCVars.NPCEnabled, SetNPCEnabled, true);
        Subs.CVar(_configManager, CCVars.NPCPathfinding, SetNPCPathfinding, true);
        Subs.CVar(_configManager, CCVars.NPCPathfindingCombatOnly, value => _pathfindingCombatOnly = value, true);
        Subs.CVar(_configManager, CCVars.NPCPathShareEnabled, SetPathShareEnabled, true);
        Subs.CVar(_configManager, CCVars.NPCPathShareRadius, value => _pathShareRadius = value, true);
        Subs.CVar(_configManager, CCVars.NPCPathShareActivationRange, value => _pathShareActivationRange = value, true);
        Subs.CVar(_configManager, CCVars.NPCPathShareTargetTolerance, value => _pathShareTargetTolerance = value, true);
        Subs.CVar(_configManager, CCVars.NPCPathShareBreakawayChance, value => _pathShareBreakawayChance = Math.Clamp(value, 0f, 1f), true);
        Subs.CVar(_configManager, CCVars.NPCPathShareBreakawayDuration, value => _pathShareBreakawayDuration = MathF.Max(0f, value), true);
        Subs.CVar(_configManager, CCVars.NPCPathShareDirectOverrideRatio, value => _pathShareDirectOverrideRatio = MathF.Max(0f, value), true);
        Subs.CVar(_configManager, CCVars.NPCPathShareNonCombatEnabled, value => _pathShareNonCombatEnabled = value, true);
        Subs.CVar(_configManager, CCVars.NPCPathShareNonCombatDynamic, value => _pathShareNonCombatDynamic = value, true);
        Subs.CVar(_configManager, CCVars.NPCPathShareNonCombatMaxSkip, value => _pathShareNonCombatMaxSkip = Math.Max(0, value), true);
        Subs.CVar(_configManager, CCVars.NPCPathShareNonCombatFlipChance, value => _pathShareNonCombatFlipChance = Math.Clamp(value, 0f, 1f), true);
        Subs.CVar(_configManager, CCVars.NPCPathShareLoopFlipEndpointTolerance, value => _pathShareLoopFlipEndpointTolerance = MathF.Max(0f, value), true);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;

        SubscribeLocalEvent<NPCSteeringComponent, ComponentShutdown>(OnSteeringShutdown);
        SubscribeNetworkEvent<RequestNPCSteeringDebugEvent>(OnDebugRequest);
    }

    private void SetNPCEnabled(bool obj)
    {
        if (!obj)
        {
            foreach (var (comp, mover) in EntityQuery<NPCSteeringComponent, InputMoverComponent>())
            {
                mover.CurTickSprintMovement = Vector2.Zero;
                CancelAndDisposePathRequest(comp);
            }

            _sharedPaths.Clear();
            _breakawayUntil.Clear();
        }

        _enabled = obj;
    }

    private void SetNPCPathfinding(bool value)
    {
        _pathfinding = value;

        if (!_pathfinding)
        {
            foreach (var comp in EntityQuery<NPCSteeringComponent>(true))
            {
                CancelAndDisposePathRequest(comp);
            }
        }
    }

    private void SetPathShareEnabled(bool value)
    {
        _pathShareEnabled = value;

        if (value)
            return;

        // Clear transient sharing state immediately when feature is disabled.
        _sharedPaths.Clear();
        _breakawayUntil.Clear();
    }

    private void OnDebugRequest(RequestNPCSteeringDebugEvent msg, EntitySessionEventArgs args)
    {
        if (!_admin.IsAdmin(args.SenderSession))
            return;

        if (msg.Enabled)
            _subscribedSessions.Add(args.SenderSession);
        else
            _subscribedSessions.Remove(args.SenderSession);
    }

    private void OnSteeringShutdown(EntityUid uid, NPCSteeringComponent component, ComponentShutdown args)
    {
        // Cancel any active pathfinding jobs as they're irrelevant.
        CancelAndDisposePathRequest(component);
        _breakawayUntil.Remove(uid);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus is SessionStatus.Disconnected or SessionStatus.Zombie)
            _subscribedSessions.Remove(e.Session);
    }

    private static void CancelAndDisposePathRequest(NPCSteeringComponent component)
    {
        var token = component.PathfindToken;
        component.PathfindToken = null;

        if (token == null)
            return;

        token.Cancel();
        token.Dispose();
    }

    /// <summary>
    /// Adds the AI to the steering system to move towards a specific target
    /// </summary>
    public NPCSteeringComponent Register(EntityUid uid, EntityCoordinates coordinates, NPCSteeringComponent? component = null)
    {
        if (Resolve(uid, ref component, false))
        {
            if (component.Coordinates.Equals(coordinates))
                return component;

            CancelAndDisposePathRequest(component);
            component.CurrentPath.Clear();
        }
        else
        {
            component = AddComp<NPCSteeringComponent>(uid);
            component.Flags = _pathfindingSystem.GetFlags(uid);
        }

        ResetStuck(component, Transform(uid).Coordinates);
        component.Coordinates = coordinates;
        return component;
    }

    /// <summary>
    /// Attempts to register the entity. Does nothing if the coordinates already registered.
    /// </summary>
    public bool TryRegister(EntityUid uid, EntityCoordinates coordinates, NPCSteeringComponent? component = null)
    {
        if (Resolve(uid, ref component, false) && component.Coordinates.Equals(coordinates))
        {
            return false;
        }

        Register(uid, coordinates, component);
        return true;
    }

    /// <summary>
    /// Stops the steering behavior for the AI and cleans up.
    /// </summary>
    public void Unregister(EntityUid uid, NPCSteeringComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (EntityManager.TryGetComponent(uid, out InputMoverComponent? controller))
        {
            controller.CurTickSprintMovement = Vector2.Zero;

            var ev = new SpriteMoveEvent(false);
            RaiseLocalEvent(uid, ref ev);
        }

        CancelAndDisposePathRequest(component);
        _breakawayUntil.Remove(uid);
        RemComp<NPCSteeringComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        if ((_pathShareEnabled || _sharedPaths.Count > 0 || _breakawayUntil.Count > 0) &&
            _timing.CurTime >= _nextSharedPathPrune)
        {
            PruneSharedPaths();
            _nextSharedPathPrune = _timing.CurTime + TimeSpan.FromSeconds(1);
        }

        var activeCount = Count<ActiveNPCComponent>();
        if (activeCount == 0)
            return;

        // Not every mob has the modifier component so do it as a separate query.
        var npcs = ArrayPool<(EntityUid, NPCSteeringComponent, InputMoverComponent, TransformComponent)>.Shared.Rent(activeCount);

        try
        {
        var query = EntityQueryEnumerator<ActiveNPCComponent, NPCSteeringComponent, InputMoverComponent, TransformComponent>();
        var index = 0;

        while (query.MoveNext(out var uid, out _, out var steering, out var mover, out var xform))
        {
            npcs[index] = (uid, steering, mover, xform);
            index++;
        }

        // Dependency issues across threads.
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 1,
        };
        var curTime = _timing.CurTime;

        _activeSteeringCount = 0;

            for (var i = 0; i < index; i++)
        {
            var (uid, steering, mover, xform) = npcs[i];
            Steer(uid, steering, mover, xform, frameTime, curTime);
            }

        ActiveSteeringGauge.Set(_activeSteeringCount);

        if (_subscribedSessions.Count > 0)
        {
            var data = new List<NPCSteeringDebugData>(index);

            for (var i = 0; i < index; i++)
            {
                var (uid, steering, mover, _) = npcs[i];

                data.Add(new NPCSteeringDebugData(
                    GetNetEntity(uid),
                    mover.CurTickSprintMovement,
                    steering.Interest,
                    steering.Danger,
                    steering.DangerPoints));
            }

            var filter = Filter.Empty();
            filter.AddPlayers(_subscribedSessions);

            RaiseNetworkEvent(new NPCSteeringDebugEvent(data), filter);
        }
    }
        finally
        {
            ArrayPool<(EntityUid, NPCSteeringComponent, InputMoverComponent, TransformComponent)>.Shared.Return(npcs, true);
        }
    }

    private bool IsActivelyChasing(EntityUid uid)
    {
        if (_npcMeleeQuery.TryComp(uid, out var melee) &&
            melee.Target.IsValid() &&
            melee.Status is not CombatStatus.NotInSight and not CombatStatus.TargetUnreachable)
        {
            return true;
        }

        if (_npcRangedQuery.TryComp(uid, out var ranged) &&
            ranged.Target.IsValid() &&
            ranged.Status != CombatStatus.NotInSight)
        {
            return true;
        }

        return false;
    }

    private bool ShouldPathfind(EntityUid uid)
    {
        // Core pathfinding should remain authoritative for NPC behavior.
        // Combat-only settings should scope optimization layers (path sharing),
        // not disable normal steering/path requests.
        return _pathfinding;
    }

    private bool ShouldUsePathSharing(EntityUid uid, out bool inCombat)
    {
        inCombat = false;

        if (!_pathShareEnabled)
            return false;

        inCombat = IsActivelyChasing(uid);

        if (inCombat)
            return true;

        if (_pathfindingCombatOnly && !_pathShareNonCombatEnabled)
            return false;

        if (!_pathfindingCombatOnly && !_pathShareNonCombatEnabled)
            return false;

        return true;
    }

    private bool TryReuseSharedPath(EntityUid uid, NPCSteeringComponent steering, TransformComponent xform)
    {
        if (!ShouldUsePathSharing(uid, out var inCombat) || _sharedPaths.Count == 0)
            return false;

        // Do not override pathing while we're already in-range for current behavior
        // (e.g. melee/ranged engagement), or while obstacle handling is in progress.
        if (steering.Status == SteeringStatus.InRange || steering.DoAfterId != null)
            return false;

        var now = _timing.CurTime;

        // Randomized short breakaway windows introduce variation while keeping shared path cost low.
        if (_breakawayUntil.TryGetValue(uid, out var until) && now < until)
            return false;

        if (_pathShareBreakawayChance > 0f && _random.Prob(_pathShareBreakawayChance))
        {
            _breakawayUntil[uid] = now + TimeSpan.FromSeconds(_pathShareBreakawayDuration);
            return false;
        }

        if (!TryGetPathGroupKey(uid, steering, out var key))
            return false;

        if (!_sharedPaths.TryGetValue(key, out var snapshot))
            return false;

        var ourMap = _transform.GetMapCoordinates(uid, xform: xform);
        var targetMap = _transform.ToMapCoordinates(steering.Coordinates);

        if (ourMap.MapId != targetMap.MapId)
            return false;

        if (now - snapshot.Timestamp > SharedPathLifetime)
            return false;

        var radiusSq = _pathShareRadius * _pathShareRadius;
        var activationRangeSq = _pathShareActivationRange * _pathShareActivationRange;
        var targetToleranceSq = _pathShareTargetTolerance * _pathShareTargetTolerance;

        if (snapshot.Path.Count == 0 || snapshot.Origin.MapId != ourMap.MapId || snapshot.Target.MapId != targetMap.MapId)
            return false;

        if ((snapshot.Origin.Position - ourMap.Position).LengthSquared() > radiusSq)
            return false;

        // Only chain while this NPC is within active chase range of the same target.
        if ((targetMap.Position - ourMap.Position).LengthSquared() > activationRangeSq)
            return false;

        if ((snapshot.Target.Position - targetMap.Position).LengthSquared() > targetToleranceSq)
            return false;

        // If direct pursuit is clearly cheaper than entering the shared route, replan independently.
        if (snapshot.Path.Count > 0)
        {
            var firstNode = _transform.ToMapCoordinates(GetCoordinates(snapshot.Path[0]));
            if (firstNode.MapId == ourMap.MapId)
            {
                var directDist = (targetMap.Position - ourMap.Position).Length();
                var entryDist = (firstNode.Position - ourMap.Position).Length();

                if (entryDist > 0.001f && directDist <= entryDist * _pathShareDirectOverrideRatio)
                {
                    _breakawayUntil[uid] = now + TimeSpan.FromSeconds(_pathShareBreakawayDuration * 0.5f);
                    return false;
                }
            }
        }

        var adoptedPath = new List<PathPoly>(snapshot.Path);

        if (!inCombat)
            ApplyNonCombatPathVariation(uid, key, ourMap, targetMap, adoptedPath);

        if (adoptedPath.Count == 0)
            return false;

        steering.CurrentPath = new Queue<PathPoly>(adoptedPath);
        steering.FailedPathCount = 0;

        // Chain propagation: a follower that reuses the path becomes a fresh local anchor.
        snapshot.Origin = ourMap;
        snapshot.Target = targetMap;
        snapshot.Timestamp = now;

        return true;
    }

    private void ApplyNonCombatPathVariation(
        EntityUid uid,
        PathGroupKey key,
        MapCoordinates ourMap,
        MapCoordinates targetMap,
        List<PathPoly> path)
    {
        if (!_pathShareNonCombatDynamic || path.Count <= 1)
            return;

        if (ShouldFlipNonCombatSharedPath(uid, key, path))
            path.Reverse();

        if (_pathShareNonCombatMaxSkip > 0 && path.Count > 1)
        {
            var maxSkips = Math.Min(_pathShareNonCombatMaxSkip, path.Count - 1);

            if (maxSkips > 0)
            {
                var hash = Math.Abs(uid.GetHashCode());
                var skipCount = hash % (maxSkips + 1);

                if (skipCount > 0)
                    path.RemoveRange(0, skipCount);
            }
        }

        if (path.Count == 0)
            return;

        // Ensure variation does not adopt a path heading away from the target.
        var first = _transform.ToMapCoordinates(GetCoordinates(path[0]));
        if (first.MapId != ourMap.MapId)
            return;

        var direct = targetMap.Position - ourMap.Position;
        var entry = first.Position - ourMap.Position;

        if (direct.LengthSquared() > 0.0001f && entry.LengthSquared() > 0.0001f && Vector2.Dot(direct, entry) < 0f)
            path.Reverse();
    }

    private bool CanFlipLoopLikePath(List<PathPoly> path)
    {
        if (path.Count < 4)
            return false;

        var first = _transform.ToMapCoordinates(GetCoordinates(path[0]));
        var last = _transform.ToMapCoordinates(GetCoordinates(path[^1]));

        if (first.MapId != last.MapId)
            return false;

        var tolerance = _pathShareLoopFlipEndpointTolerance;
        return (first.Position - last.Position).LengthSquared() <= tolerance * tolerance;
    }

    private bool ShouldFlipNonCombatSharedPath(EntityUid uid, PathGroupKey key, List<PathPoly> path)
    {
        if (_pathShareNonCombatFlipChance <= 0f || !CanFlipLoopLikePath(path))
            return false;

        // Use a deterministic roll per NPC + path group to avoid rapid flip/no-flip oscillation.
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + uid.GetHashCode();
            hash = hash * 31 + key.TargetUid.GetHashCode();
            hash = hash * 31 + (int)key.MapId;
            hash = hash * 31 + (int)key.Flags;
            hash = hash * 31 + path.Count;

            var roll = (uint)hash % 10000u;
            return roll < _pathShareNonCombatFlipChance * 10000f;
        }
    }

    private bool TryGetPathGroupKey(EntityUid uid, NPCSteeringComponent steering, out PathGroupKey key)
    {
        key = default;
        var targetUid = steering.Coordinates.EntityId;

        if (!targetUid.IsValid() || Deleted(targetUid))
            return false;

        var ourMap = _transform.GetMapCoordinates(uid);
        var targetMap = _transform.ToMapCoordinates(steering.Coordinates);

        if (ourMap.MapId != targetMap.MapId)
            return false;

        key = new PathGroupKey(targetUid, ourMap.MapId, steering.Flags);
        return true;
    }

    private void PruneSharedPaths()
    {
        _prunePathGroups.Clear();
        var now = _timing.CurTime;

        foreach (var (key, snapshot) in _sharedPaths)
        {
            if (now - snapshot.Timestamp > SharedPathLifetime || Deleted(key.TargetUid))
                _prunePathGroups.Add(key);
        }

        foreach (var key in _prunePathGroups)
        {
            _sharedPaths.Remove(key);
        }

        _pruneBreakaway.Clear();
        foreach (var (uid, until) in _breakawayUntil)
        {
            if (now >= until || Deleted(uid))
                _pruneBreakaway.Add(uid);
        }

        foreach (var uid in _pruneBreakaway)
        {
            _breakawayUntil.Remove(uid);
        }
    }

    private void SetDirection(EntityUid uid, InputMoverComponent component, NPCSteeringComponent steering, Vector2 value, bool clear = true)
    {
        if (clear && value.Equals(Vector2.Zero))
        {
            steering.CurrentPath.Clear();
            Array.Clear(steering.Interest);
            Array.Clear(steering.Danger);
        }

        component.CurTickSprintMovement = value;
        component.LastInputTick = _timing.CurTick;
        component.LastInputSubTick = ushort.MaxValue;

        var ev = new SpriteMoveEvent(true);
        RaiseLocalEvent(uid, ref ev);
    }

    /// <summary>
    /// Go through each steerer and combine their vectors
    /// </summary>
    private void Steer(
        EntityUid uid,
        NPCSteeringComponent steering,
        InputMoverComponent mover,
        TransformComponent xform,
        float frameTime,
        TimeSpan curTime)
    {
        if (Deleted(steering.Coordinates.EntityId))
        {
            SetDirection(uid, mover, steering, Vector2.Zero);
            steering.Status = SteeringStatus.NoPath;
            return;
        }

        // No path set from pathfinding or the likes.
        if (steering.Status == SteeringStatus.NoPath)
        {
            SetDirection(uid, mover, steering, Vector2.Zero);
            return;
        }

        // Can't move at all, just noop input.
        if (!mover.CanMove)
        {
            SetDirection(uid, mover, steering, Vector2.Zero);
            steering.Status = SteeringStatus.NoPath;
            return;
        }

        Interlocked.Increment(ref _activeSteeringCount);

        var agentRadius = steering.Radius;
        var worldPos = _transform.GetWorldPosition(xform);
        var (layer, mask) = _physics.GetHardCollision(uid);

        // Use rotation relative to parent to rotate our context vectors by.
        var offsetRot = -_mover.GetParentGridAngle(mover);
        _modifierQuery.TryGetComponent(uid, out var modifier);
        var body = _physicsQuery.GetComponent(uid);
        // Monolith - early port of wizden#38846
        var weightless = _gravity.IsWeightless(uid);
        var moveSpeed = GetSprintSpeed(uid, modifier);
        var acceleration = GetAcceleration((uid, modifier), weightless);
        var friction = GetFriction((uid, modifier), weightless);

        var dangerPoints = steering.DangerPoints;
        dangerPoints.Clear();
        Span<float> interest = stackalloc float[InterestDirections];
        Span<float> danger = stackalloc float[InterestDirections];

        // TODO: This should be fly
        steering.CanSeek = true;

        var ev = new NPCSteeringEvent(steering, xform, worldPos, offsetRot);
        RaiseLocalEvent(uid, ref ev);
        // If seek has arrived at the target node for example then immediately re-steer.
        var forceSteer = true;
        var moveMultiplier = 1f; // Monolith - multiplier to acceleration we should actually move with

        if (steering.CanSeek && !TrySeek(uid, mover, steering, body, xform, offsetRot, moveSpeed, acceleration, friction, interest, frameTime, ref forceSteer, ref moveMultiplier))
        {
            SetDirection(uid, mover, steering, Vector2.Zero);
            return;
        }

        DebugTools.Assert(!float.IsNaN(interest[0]));

        // Don't steer too frequently to avoid twitchiness.
        // This should also implicitly solve tie situations.
        // I think doing this after all the ops above is best?
        // Originally I had it way above but sometimes mobs would overshoot their tile targets.

        if (!forceSteer)
        {
            SetDirection(uid, mover, steering, steering.LastSteerDirection, false);
            return;
        }

        // Avoid static objects like walls
        CollisionAvoidance(uid, offsetRot, worldPos, agentRadius, layer, mask, xform, danger);
        DebugTools.Assert(!float.IsNaN(danger[0]));

        Separation(uid, offsetRot, worldPos, agentRadius, layer, mask, body, xform, danger);

        // Blend last and current tick
        Blend(steering, frameTime, interest, danger);

        // Remove the danger map from the interest map.
        var desiredDirection = -1;
        var desiredValue = 0f;

        for (var i = 0; i < InterestDirections; i++)
        {
            var adjustedValue = Math.Clamp(steering.Interest[i] - steering.Danger[i], 0f, 1f);

            if (adjustedValue > desiredValue)
            {
                desiredDirection = i;
                desiredValue = adjustedValue;
            }
        }

        var resultDirection = Vector2.Zero;

        if (desiredDirection != -1)
        {
            resultDirection = new Angle(desiredDirection * InterestRadians).ToVec() * moveMultiplier; // Monolith
        }

        steering.LastSteerDirection = resultDirection;
        DebugTools.Assert(!float.IsNaN(resultDirection.X));
        SetDirection(uid, mover, steering, resultDirection, false);
    }

    private EntityCoordinates GetCoordinates(PathPoly poly)
    {
        if (!poly.IsValid())
            return EntityCoordinates.Invalid;

        return new EntityCoordinates(poly.GraphUid, poly.Box.Center);
    }

    /// <summary>
    /// Get a new job from the pathfindingsystem
    /// </summary>
    private async void RequestPath(EntityUid uid, NPCSteeringComponent steering, TransformComponent xform, float targetDistance)
    {
        // If we already have a pathfinding request then don't grab another.
        // If we're in range then just beeline them; this can avoid stutter stepping and is an easy way to look nicer.
        if (steering.Pathfind || targetDistance < steering.RepathRange)
            return;

        // Short-circuit with no path.
        var targetPoly = _pathfindingSystem.GetPoly(steering.Coordinates);

        // If this still causes issues future sloth adjust the collision mask.
        // Thanks past sloth I already realised.
        if (targetPoly != null &&
            steering.Coordinates.Position.Equals(Vector2.Zero) &&
            TryComp<PhysicsComponent>(uid, out var physics) &&
            _interaction.InRangeUnobstructed(uid, steering.Coordinates.EntityId, range: 30f, (CollisionGroup)physics.CollisionMask))
        {
            steering.CurrentPath.Clear();
            steering.CurrentPath.Enqueue(targetPoly);
            return;
        }

        var pathToken = new CancellationTokenSource();
        steering.PathfindToken = pathToken;

        var flags = _pathfindingSystem.GetFlags(uid);

        PathResultEvent result;
        try
        {
            result = await _pathfindingSystem.GetPathSafe(
            uid,
            xform.Coordinates,
            steering.Coordinates,
            steering.Range,
                pathToken.Token,
            flags);
        }
        finally
        {
            if (ReferenceEquals(steering.PathfindToken, pathToken))
        steering.PathfindToken = null;

            pathToken.Dispose();
        }

        if (pathToken.IsCancellationRequested)
            return;

        if (result.Result == PathResult.NoPath)
        {
            steering.CurrentPath.Clear();
            steering.FailedPathCount++;

            if (steering.FailedPathCount >= NPCSteeringComponent.FailedPathLimit)
            {
                steering.Status = SteeringStatus.NoPath;
            }

            return;
        }

        var targetPos = _transform.ToMapCoordinates(steering.Coordinates);
        var ourPos = _transform.GetMapCoordinates(uid, xform: xform);

        PrunePath(uid, ourPos, targetPos.Position - ourPos.Position, result.Path);
        steering.CurrentPath = new Queue<PathPoly>(result.Path);

        if (ShouldUsePathSharing(uid, out _) && TryGetPathGroupKey(uid, steering, out var key))
        {
            _sharedPaths[key] = new SharedPathSnapshot
            {
                Origin = ourPos,
                Target = targetPos,
                Path = new List<PathPoly>(result.Path),
                Timestamp = _timing.CurTime,
            };
        }
    }

    // TODO: Move these to movercontroller

    private float GetSprintSpeed(EntityUid uid, MovementSpeedModifierComponent? modifier = null)
    {
        if (!Resolve(uid, ref modifier, false))
        {
            return MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
        }

        return modifier.CurrentSprintSpeed;
    }

    // <Monolith> - early port of wizden#38846
    private float GetAcceleration(Entity<MovementSpeedModifierComponent?> ent, bool weightless)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return weightless ? MovementSpeedModifierComponent.DefaultWeightlessAcceleration : MovementSpeedModifierComponent.DefaultAcceleration;

        return weightless ? ent.Comp.WeightlessAcceleration : ent.Comp.Acceleration;
    }

    private float GetFriction(Entity<MovementSpeedModifierComponent?> ent, bool weightless)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return weightless ? MovementSpeedModifierComponent.DefaultWeightlessFriction : MovementSpeedModifierComponent.DefaultFriction;

        return weightless ? ent.Comp.WeightlessFriction : ent.Comp.Friction;
    }
    // </Monolith>
    public override void Shutdown()
    {
        base.Shutdown();
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
        _subscribedSessions.Clear();
        _sharedPaths.Clear();
        _breakawayUntil.Clear();
    }
}
