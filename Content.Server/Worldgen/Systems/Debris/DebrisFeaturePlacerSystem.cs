using System.Linq;
using System.Numerics;
using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Components.Debris;
using Content.Server.Worldgen.Systems.GC;
using Content.Server.Worldgen.Tools;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Server._NF.Worldgen.Components.Debris; // Frontier
using Content.Shared.CCVar;
using Robust.Shared.Timing;

namespace Content.Server.Worldgen.Systems.Debris;

/// <summary>
///     This handles placing debris within the world evenly with rng, primarily for structures like asteroid fields.
/// </summary>
public sealed class DebrisFeaturePlacerSystem : BaseWorldSystem
{
    [Dependency] private readonly GCQueueSystem _gc = default!;
    [Dependency] private readonly NoiseIndexSystem _noiseIndex = default!;
    [Dependency] private readonly PoissonDiskSampler _sampler = default!;
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private const float IdleDebrisLinearVelocityEpsilon = 0.05f;
    private const float IdleDebrisAngularVelocityEpsilon = 0.01f;

    private ISawmill _sawmill = default!;

    private Queue<DebrisFeaturePlacerControllerComponent> _debrisQ = new();

    private List<Entity<MapGridComponent>> _mapGrids = new();
    private int _maxSpawnsPerTick = 1;
    private int _maxDeSpawnsPerTick = 1;
    private int _deSpawnsThisTick = 0;
    private int _spawnsThisTick = 0;
    private TimeSpan _updateDelay = TimeSpan.FromSeconds(1);
    private TimeSpan _nextUpdate = TimeSpan.Zero;

    /// <inheritdoc />
    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("world.debris.feature_placer");
        SubscribeLocalEvent<DebrisFeaturePlacerControllerComponent, WorldChunkLoadedEvent>(OnChunkLoaded);
        SubscribeLocalEvent<DebrisFeaturePlacerControllerComponent, WorldChunkUnloadedEvent>(OnChunkUnloaded);
        SubscribeLocalEvent<OwnedDebrisComponent, ComponentShutdown>(OnDebrisShutdown);
        SubscribeLocalEvent<OwnedDebrisComponent, MoveEvent>(OnDebrisMove);
        SubscribeLocalEvent<OwnedDebrisComponent, TryCancelGC>(OnTryCancelGC);
        SubscribeLocalEvent<SimpleDebrisSelectorComponent, TryGetPlaceableDebrisFeatureEvent>(
            OnTryGetPlacableDebrisEvent);

        _cfg.OnValueChanged(CCVars.DebrisMaxSpawnsPerTick, v => _maxSpawnsPerTick = v, true);
        _cfg.OnValueChanged(CCVars.DebrisMaxDeSpawnsPerTick, v => _maxDeSpawnsPerTick = v, true);
        _cfg.OnValueChanged(CCVars.DebrisDelayBetweenUpdates, v => _updateDelay = TimeSpan.FromSeconds(v), true);
    }

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Enforce update delay
        var curTime = _timing.CurTime;
        if (curTime < _nextUpdate)
            return;
        _nextUpdate = curTime + _updateDelay;
        _deSpawnsThisTick = 0;
        _spawnsThisTick = 0;

        if (_debrisQ.Count <= 0)
        {
            var query = EntityQueryEnumerator<DebrisFeaturePlacerControllerComponent>();
            while (query.MoveNext(out var uid, out var component))
            {
                _debrisQ.Enqueue(component);
            }
        }
        while (_debrisQ.Count > 0)
        {
            if (_spawnsThisTick >= _maxSpawnsPerTick
                && _deSpawnsThisTick >= _maxDeSpawnsPerTick)
                break;
            if (!_debrisQ.TryDequeue(out var component))
                break;
            if (component.Deleted)
                continue;
            ProcessPendingDeSpawns(component);
            ProcessPendingSpawns(component);
        }
    }

    /// <summary>
    ///     Processes queued debris spawns gradually to avoid lag spikes.
    /// </summary>
    private void ProcessPendingSpawns(DebrisFeaturePlacerControllerComponent component)
    {
        if (_maxSpawnsPerTick <= 0)
            return;
        if (_spawnsThisTick >= _maxSpawnsPerTick)
            return;
        while (component.PendingSpawns.TryDequeue(out var pending)
               && _spawnsThisTick < _maxSpawnsPerTick)
        {
            // Skip if already exists or chunk is gone
            if (component.OwnedDebris.ContainsKey(pending.Point)
                || Deleted(pending.ChunkUid))
            {
                continue;
            }

            var ent = Spawn(pending.DebrisProto, pending.Coords);
            component.OwnedDebris.Add(pending.Point, ent);

            var owned = EnsureComp<OwnedDebrisComponent>(ent);
            owned.OwningController = pending.ControllerUid;
            owned.LastKey = pending.Point;

            EnsureComp<SpaceDebrisComponent>(ent); // Frontier
            TrySleepDebris(ent);

            _spawnsThisTick++;
        }
    }

    private void TrySleepDebris(EntityUid uid)
    {
        if (!TryComp<PhysicsComponent>(uid, out var body))
            return;

        if (body.BodyType != BodyType.Dynamic)
            return;

        _physics.SetSleepingAllowed(uid, body, true);

        if (body.Awake &&
            body.LinearVelocity.LengthSquared() <= IdleDebrisLinearVelocityEpsilon * IdleDebrisLinearVelocityEpsilon &&
            MathF.Abs(body.AngularVelocity) <= IdleDebrisAngularVelocityEpsilon)
        {
            _physics.SetAwake(uid, body, false);
        }
    }

    /// <summary>
    ///     Processes queued debris despawns gradually to avoid lag spikes.
    /// </summary>
    private void ProcessPendingDeSpawns(DebrisFeaturePlacerControllerComponent component)
    {
        if (_maxDeSpawnsPerTick <= 0)
            return;
        if (_deSpawnsThisTick >= _maxDeSpawnsPerTick)
            return;
        while (component.PendingDeSpawns.TryPeek(out var debrisTuple)
               && _deSpawnsThisTick < _maxDeSpawnsPerTick)
        {
            var vect = debrisTuple.Item1;
            var debris = debrisTuple.Item2;
            var chunk = debrisTuple.Item3;
            if (Deleted(debris))
            {
                component.PendingDeSpawns.Dequeue();
                component.OwnedDebris.Remove(vect);
                component.DoSpawns = true;
                continue;
            }
            if (HasComp<LoadedChunkComponent>(chunk))
            {
                break; // Can't despawn while loaded
            }
            _deSpawnsThisTick++;
            QueueDel(debris);
            component.PendingDeSpawns.Dequeue();
            component.OwnedDebris.Remove(vect);
            component.DoSpawns = true;
        }
    }

    /// <summary>
    ///     Handles GC cancellation in case the chunk is still loaded.
    /// </summary>
    private void OnTryCancelGC(EntityUid uid, OwnedDebrisComponent component, ref TryCancelGC args)
    {
        args.Cancelled |= HasComp<LoadedChunkComponent>(component.OwningController);
    }

    /// <summary>
    ///     Handles debris moving, and making sure it stays parented to a chunk for loading purposes.
    /// </summary>
    private void OnDebrisMove(EntityUid uid, OwnedDebrisComponent component, ref MoveEvent args)
    {
        if (!HasComp<WorldChunkComponent>(component.OwningController))
            return; // Redundant logic, prolly needs it's own handler for your custom system.

        var xform = args.Component;
        var ownerXform = Transform(component.OwningController);

        // Early exit checks - avoid unnecessary work
        if (xform.MapUid is null || ownerXform.MapUid is null)
            return; // not our problem

        if (xform.MapUid != ownerXform.MapUid)
        {
            _sawmill.Error($"Somehow debris {uid} left it's expected map! Unparenting it to avoid issues.");
            var placer = Comp<DebrisFeaturePlacerControllerComponent>(component.OwningController);
            RemCompDeferred<OwnedDebrisComponent>(uid);
            placer.OwnedDebris.Remove(component.LastKey);
            return;
        }

        // Check if debris actually crossed chunk boundaries - skip dictionary updates if not
        var newChunkCoords = GetChunkCoords(uid);
        var oldChunkCoords = WorldGen.WorldToChunkCoords(component.LastKey);

        if (newChunkCoords == oldChunkCoords)
            return; // Still in same chunk, no update needed

        var oldPlacer = Comp<DebrisFeaturePlacerControllerComponent>(component.OwningController);
        oldPlacer.OwnedDebris.Remove(component.LastKey);

        var newChunk = GetOrCreateChunk(newChunkCoords, xform.MapUid!.Value);
        if (newChunk is null || !TryComp<DebrisFeaturePlacerControllerComponent>(newChunk, out var newPlacer))
        {
            // Whelp.
            RemCompDeferred<OwnedDebrisComponent>(uid);
            return;
        }

        newPlacer.OwnedDebris[_xformSys.GetWorldPosition(xform)] = uid; // Change our owner.
        component.OwningController = newChunk.Value;
    }

    /// <summary>
    ///     Handles debris shutdown/detach.
    /// </summary>
    private void OnDebrisShutdown(EntityUid uid, OwnedDebrisComponent component, ComponentShutdown args)
    {
        if (!TryComp<DebrisFeaturePlacerControllerComponent>(component.OwningController, out var placer))
            return;

        placer.OwnedDebris[component.LastKey] = null;
        if (Terminating(uid))
            placer.OwnedDebris.Remove(component.LastKey);
    }

    /// <summary>
    ///     Queues all debris owned by the placer for garbage collection.
    /// </summary>
    private void OnChunkUnloaded(EntityUid uid, DebrisFeaturePlacerControllerComponent component,
        ref WorldChunkUnloadedEvent args)
    {
        foreach (var (vector, debris) in component.OwnedDebris)
        {
            if (debris is not null)
            {
                component.PendingDeSpawns.Enqueue((vector, debris.Value, args.Chunk));
                // _gc.TryGCEntity(debris.Value); // gonb.
            }
        }
        // component.DoSpawns = true;
    }

    /// <summary>
    ///     Handles providing a debris type to place for SimpleDebrisSelectorComponent.
    ///     This randomly picks a debris type from the EntitySpawnCollectionCache.
    /// </summary>
    private void OnTryGetPlacableDebrisEvent(EntityUid uid, SimpleDebrisSelectorComponent component,
        ref TryGetPlaceableDebrisFeatureEvent args)
    {
        if (args.DebrisProto is not null)
            return;

        var l = new List<string?>(1);
        component.CachedDebrisTable.GetSpawns(_random, ref l);

        switch (l.Count)
        {
            case 0:
                return;
            case > 1:
                _sawmill.Warning($"Got more than one possible debris type from {uid}. List: {string.Join(", ", l)}");
                break;
        }

        args.DebrisProto = l[0];
    }

    /// <summary>
    ///     Handles loading in debris. This does the following:
    ///     - Checks if the debris is currently supposed to do spawns, if it isn't, aborts immediately.
    ///     - Evaluates the density value to be used for placement, if it's zero, aborts.
    ///     - Generates the points to generate debris at, if and only if they've not been selected already by a prior load.
    ///     - Queues debris for deferred spawning across multiple ticks to avoid lag spikes.
    /// </summary>
    private void OnChunkLoaded(EntityUid uid, DebrisFeaturePlacerControllerComponent component,
        ref WorldChunkLoadedEvent args)
    {
        // if our things were scheduled for despawn, cancel that, chunk is loaded again
        component.PendingDeSpawns.Clear();

        if (component.DoSpawns == false)
            return;

        component.DoSpawns = false; // Don't repeat yourself if this crashes.

        if (!TryComp<WorldChunkComponent>(args.Chunk, out var chunk))
            return;

        var chunkMap = chunk.Map;

        if (!TryComp<MapComponent>(chunkMap, out var map))
            return;

        var densityChannel = component.DensityNoiseChannel;
        var density = _noiseIndex.Evaluate(uid, densityChannel, chunk.Coordinates + new Vector2(0.5f, 0.5f));
        if (density == 0)
            return;

        List<Vector2>? points = null;

        // If we've been loaded before, reuse the same coordinates.
        if (component.OwnedDebris.Count != 0)
        {
            // Manual iteration instead of LINQ to reduce allocations
            points = new List<Vector2>(component.OwnedDebris.Count);
            foreach (var (key, value) in component.OwnedDebris)
            {
                if (!Deleted(value))
                    points.Add(key);
            }
        }

        points ??= GeneratePointsInChunk(args.Chunk, density, chunk.Coordinates, chunkMap);

        var mapId = map.MapId;

        var safetyBounds = Box2.UnitCentered.Enlarged(component.SafetyZoneRadius);
        var failures = 0; // Avoid severe log spam.

        foreach (var point in points)
        {
            if (component.OwnedDebris.TryGetValue(point, out var existing))
            {
                DebugTools.Assert(Exists(existing));
                continue;
            }

            var pointDensity = _noiseIndex.Evaluate(uid, densityChannel, WorldGen.WorldToChunkCoords(point));
            if (pointDensity == 0 && component.DensityClip || _random.Prob(component.RandomCancellationChance))
                continue;

            if (HasCollisions(mapId, safetyBounds.Translated(point)))
                continue;

            var coords = new EntityCoordinates(chunkMap, point);

            var preEv = new PrePlaceDebrisFeatureEvent(coords, args.Chunk);
            RaiseLocalEvent(uid, ref preEv);
            if (uid != args.Chunk)
                RaiseLocalEvent(args.Chunk, ref preEv);

            if (preEv.Handled)
                continue;

            var debrisFeatureEv = new TryGetPlaceableDebrisFeatureEvent(coords, args.Chunk);
            RaiseLocalEvent(uid, ref debrisFeatureEv);

            if (debrisFeatureEv.DebrisProto == null)
            {
                // Try on the chunk...?
                if (uid != args.Chunk)
                    RaiseLocalEvent(args.Chunk, ref debrisFeatureEv);

                if (debrisFeatureEv.DebrisProto == null)
                {
                    // Nope.
                    failures++;
                    continue;
                }
            }

            // Queue the spawn instead of spawning immediately - spreads load across ticks
            component.PendingSpawns.Enqueue(new PendingDebrisSpawn
            {
                Point = point,
                DebrisProto = debrisFeatureEv.DebrisProto,
                Coords = coords,
                ControllerUid = uid,
                ChunkUid = args.Chunk
            });
        }

        if (failures > 0)
            _sawmill.Error($"Failed to place {failures} debris at chunk {args.Chunk}");
    }

    /// <summary>
    /// Checks to see if the potential spawn point is clear
    /// </summary>
    /// <param name="mapId"></param>
    /// <param name="point"></param>
    /// <returns></returns>
    private bool HasCollisions(MapId mapId, Box2 point)
    {
        _mapGrids.Clear();
        _mapManager.FindGridsIntersecting(mapId, point, ref _mapGrids);
        return _mapGrids.Count > 0;
    }

    /// <summary>
    ///     Generates the points to put into a chunk using a poisson disk sampler.
    /// </summary>
    private List<Vector2> GeneratePointsInChunk(EntityUid chunk, float density, Vector2 coords, EntityUid map)
    {
        var offs = (int) ((WorldGen.ChunkSize - WorldGen.ChunkSize / 8.0f) / 2.0f);
        var topLeft = new Vector2(-offs, -offs);
        var lowerRight = new Vector2(offs, offs);
        var enumerator = _sampler.SampleRectangle(topLeft, lowerRight, density);
        var debrisPoints = new List<Vector2>();

        var realCenter = WorldGen.ChunkToWorldCoordsCentered(coords.Floored());

        while (enumerator.MoveNext(out var debrisPoint))
        {
            debrisPoints.Add(realCenter + debrisPoint.Value);
        }

        return debrisPoints;
    }
}

/// <summary>
///     Fired directed on the debris feature placer controller and the chunk, ahead of placing a debris piece.
/// </summary>
[ByRefEvent]
[PublicAPI]
public record struct PrePlaceDebrisFeatureEvent(EntityCoordinates Coords, EntityUid Chunk, bool Handled = false);

/// <summary>
///     Fired directed on the debris feature placer controller and the chunk, to select which debris piece to place.
/// </summary>
[ByRefEvent]
[PublicAPI]
public record struct TryGetPlaceableDebrisFeatureEvent(EntityCoordinates Coords, EntityUid Chunk,
    string? DebrisProto = null);

