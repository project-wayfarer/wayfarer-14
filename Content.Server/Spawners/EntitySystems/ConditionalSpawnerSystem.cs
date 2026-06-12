using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Shared.EntityTable;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems
{
    [UsedImplicitly]
    public sealed class ConditionalSpawnerSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _robustRandom = default!;
        [Dependency] private readonly GameTicker _ticker = default!;
        [Dependency] private readonly EntityTableSystem _entityTable = default!;

        // Wayfarer: per-grid deferred spawner queues for dungeon generation hitching reduction
        private readonly Dictionary<EntityUid, List<EntityUid>> _deferredByGrid = new();
        // End Wayfarer

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GameRuleStartedEvent>(OnRuleStarted);
            SubscribeLocalEvent<ConditionalSpawnerComponent, MapInitEvent>(OnCondSpawnMapInit);
            SubscribeLocalEvent<RandomSpawnerComponent, MapInitEvent>(OnRandSpawnMapInit);
            SubscribeLocalEvent<EntityTableSpawnerComponent, MapInitEvent>(OnEntityTableSpawnMapInit);
        }

        // Wayfarer: deferred spawner API for DungeonJob
        /// <summary>
        /// Registers a dungeon grid for deferred spawning. All spawner entities placed on this
        /// grid will be queued rather than firing immediately on MapInit.
        /// Call <see cref="FlushNext"/> in a loop (with yields) then <see cref="ClearDeferred"/>
        /// once dungeon generation is complete.
        /// </summary>
        public void BeginDeferred(EntityUid gridUid)
        {
            _deferredByGrid[gridUid] = new List<EntityUid>();
        }

        /// <summary>
        /// Processes one deferred spawner entity for the given grid.
        /// Returns true if more remain, false when the queue is empty.
        /// </summary>
        public bool FlushNext(EntityUid gridUid)
        {
            if (!_deferredByGrid.TryGetValue(gridUid, out var list) || list.Count == 0)
                return false;

            var uid = list[^1];
            list.RemoveAt(list.Count - 1);

            if (TryComp<EntityTableSpawnerComponent>(uid, out var tableComp))
            {
                Spawn((uid, tableComp));
                if (tableComp.DeleteSpawnerAfterSpawn && !TerminatingOrDeleted(uid) && Exists(uid))
                    QueueDel(uid);
            }
            else if (TryComp<RandomSpawnerComponent>(uid, out var randComp))
            {
                Spawn(uid, randComp);
                if (randComp.DeleteSpawnerAfterSpawn)
                    QueueDel(uid);
            }
            else if (TryComp<ConditionalSpawnerComponent>(uid, out var condComp))
            {
                TrySpawn(uid, condComp);
            }

            return list.Count > 0;
        }

        /// <summary>
        /// Removes the deferred queue for a grid without spawning remaining items.
        /// Always call this after <see cref="FlushNext"/> drains the queue (or on cancellation).
        /// </summary>
        public void ClearDeferred(EntityUid gridUid)
        {
            _deferredByGrid.Remove(gridUid);
        }

        private bool TryDefer(EntityUid uid)
        {
            if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid is not { } grid)
                return false;
            if (!_deferredByGrid.TryGetValue(grid, out var list))
                return false;
            list.Add(uid);
            return true;
        }
        // End Wayfarer

        private void OnCondSpawnMapInit(EntityUid uid, ConditionalSpawnerComponent component, MapInitEvent args)
        {
            // Wayfarer: defer on dungeon grids
            if (TryDefer(uid))
                return;
            // End Wayfarer
            TrySpawn(uid, component);
        }

        private void OnRandSpawnMapInit(EntityUid uid, RandomSpawnerComponent component, MapInitEvent args)
        {
            // Wayfarer: defer on dungeon grids
            if (TryDefer(uid))
                return;
            // End Wayfarer
            Spawn(uid, component);
            if (component.DeleteSpawnerAfterSpawn)
                QueueDel(uid);
        }

        private void OnEntityTableSpawnMapInit(Entity<EntityTableSpawnerComponent> ent, ref MapInitEvent args)
        {
            // Wayfarer: defer on dungeon grids
            if (TryDefer(ent))
                return;
            // End Wayfarer
            Spawn(ent);
            if (ent.Comp.DeleteSpawnerAfterSpawn && !TerminatingOrDeleted(ent) && Exists(ent))
                QueueDel(ent);
        }

        private void OnRuleStarted(ref GameRuleStartedEvent args)
        {
            var query = EntityQueryEnumerator<ConditionalSpawnerComponent>();
            while (query.MoveNext(out var uid, out var spawner))
            {
                RuleStarted(uid, spawner, args);
            }
        }

        public void RuleStarted(EntityUid uid, ConditionalSpawnerComponent component, GameRuleStartedEvent obj)
        {
            if (component.GameRules.Contains(obj.RuleId))
                Spawn(uid, component);
        }

        private void TrySpawn(EntityUid uid, ConditionalSpawnerComponent component)
        {
            if (component.GameRules.Count == 0)
            {
                Spawn(uid, component);
                return;
            }

            foreach (var rule in component.GameRules)
            {
                if (!_ticker.IsGameRuleActive(rule))
                    continue;
                Spawn(uid, component);
                return;
            }
        }

        private void Spawn(EntityUid uid, ConditionalSpawnerComponent component)
        {
            if (component.Chance != 1.0f && !_robustRandom.Prob(component.Chance))
                return;

            if (component.Prototypes.Count == 0)
            {
                Log.Warning($"Prototype list in ConditionalSpawnComponent is empty! Entity: {ToPrettyString(uid)}");
                return;
            }

            if (!Deleted(uid))
                Spawn(_robustRandom.Pick(component.Prototypes), Transform(uid).Coordinates);
        }

        private void Spawn(EntityUid uid, RandomSpawnerComponent component)
        {
            if (component.RarePrototypes.Count > 0 && (component.RareChance == 1.0f || _robustRandom.Prob(component.RareChance)))
            {
                Spawn(_robustRandom.Pick(component.RarePrototypes), Transform(uid).Coordinates);
                return;
            }

            if (component.Chance != 1.0f && !_robustRandom.Prob(component.Chance))
                return;

            if (component.Prototypes.Count == 0)
            {
                Log.Warning($"Prototype list in RandomSpawnerComponent is empty! Entity: {ToPrettyString(uid)}");
                return;
            }

            if (Deleted(uid))
                return;

            var offset = component.Offset;
            var xOffset = _robustRandom.NextFloat(-offset, offset);
            var yOffset = _robustRandom.NextFloat(-offset, offset);

            var coordinates = Transform(uid).Coordinates.Offset(new Vector2(xOffset, yOffset));

            Spawn(_robustRandom.Pick(component.Prototypes), coordinates);
        }

        private void Spawn(Entity<EntityTableSpawnerComponent> ent)
        {
            if (TerminatingOrDeleted(ent) || !Exists(ent))
                return;

            var coords = Transform(ent).Coordinates;

            var spawns = _entityTable.GetSpawns(ent.Comp.Table);
            foreach (var proto in spawns)
            {
                var xOffset = _robustRandom.NextFloat(-ent.Comp.Offset, ent.Comp.Offset);
                var yOffset = _robustRandom.NextFloat(-ent.Comp.Offset, ent.Comp.Offset);
                var trueCoords = coords.Offset(new Vector2(xOffset, yOffset));

                SpawnAtPosition(proto, trueCoords);
            }
        }
    }
}
