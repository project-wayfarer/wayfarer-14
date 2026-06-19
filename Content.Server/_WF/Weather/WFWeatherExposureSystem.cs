using Content.Shared._WF.Weather;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._WF.Weather;

public sealed class WFWeatherExposureSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<BlockWeatherComponent> _blockQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    private readonly HashSet<EntityUid> _dirtyGrids = new();
    private readonly List<EntityUid> _rebuildBuffer = new();
    private readonly Queue<Vector2i> _bfsQueue = new();
    private readonly HashSet<Vector2i> _bfsVisited = new();

    private bool _weatherActive;
    private TimeSpan _nextUpdate;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private const int MaxRebuildsPerUpdate = 2;
    private readonly Dictionary<EntityUid, HashSet<Vector2i>> _builtFromEmpty = new();
    private readonly HashSet<EntityUid> _baselined = new();
    private readonly HashSet<Vector2i> _oldExposed = new();

    private readonly Dictionary<EntityUid, List<(Vector2i Pos, bool Opened)>> _pendingChanges = new();

    private static readonly Vector2i[] Cardinals =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    };

    public override void Initialize()
    {
        base.Initialize();

        _blockQuery = GetEntityQuery<BlockWeatherComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<BlockWeatherComponent, AnchorStateChangedEvent>(OnBlockWeatherAnchor);
        SubscribeLocalEvent<BlockWeatherComponent, MapInitEvent>(OnBlockWeatherMapInit);
    }

    private void OnGridInit(GridInitializeEvent ev)
    {
        if (!_weatherActive)
            return;
        _dirtyGrids.Add(ev.EntityUid);
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        if (!_weatherActive)
            return;

        var gridUid = ev.Entity.Owner;

        foreach (var change in ev.Changes)
        {
            if (!change.EmptyChanged)
                continue;

            QueueChange(gridUid, change.GridIndices, true);

            if (change.OldTile.IsEmpty && !change.NewTile.IsEmpty)
            {
                if (!_builtFromEmpty.TryGetValue(gridUid, out var built))
                {
                    built = new HashSet<Vector2i>();
                    _builtFromEmpty[gridUid] = built;
                }
                built.Add(change.GridIndices);
            }
        }
    }

    private void OnBlockWeatherAnchor(Entity<BlockWeatherComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!_weatherActive)
            return;

        var xform = args.Transform;
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryGetComponent(gridUid, out var grid))
            return;

        var pos = _mapSystem.TileIndicesFor(gridUid, grid, xform.Coordinates);
        QueueChange(gridUid, pos, !args.Anchored);
    }

    private void OnBlockWeatherMapInit(Entity<BlockWeatherComponent> ent, ref MapInitEvent args)
    {
        if (!_weatherActive)
            return;

        var xform = Transform(ent.Owner);
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryGetComponent(gridUid, out var grid))
            return;
        if (!xform.Anchored)
            return;

        var pos = _mapSystem.TileIndicesFor(gridUid, grid, xform.Coordinates);
        QueueChange(gridUid, pos, false);
    }

    private void QueueChange(EntityUid gridUid, Vector2i pos, bool opened)
    {
        if (!_pendingChanges.TryGetValue(gridUid, out var list))
        {
            list = new List<(Vector2i, bool)>();
            _pendingChanges[gridUid] = list;
        }
        list.Add((pos, opened));
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;
        _nextUpdate = now + UpdateInterval;

        var active = AnyWeatherActive();

        if (active && !_weatherActive)
            MarkWeatherGridsDirty();

        if (!active && _weatherActive)
            ClearAll();

        _weatherActive = active;

        if (!active)
            return;

        foreach (var (gridUid, changes) in _pendingChanges)
        {
            if (_dirtyGrids.Contains(gridUid))
                continue;
            if (!_gridQuery.TryGetComponent(gridUid, out var grid))
                continue;
            if (!TryComp<WFExposureComponent>(gridUid, out var comp))
                continue;

            var changed = false;
            foreach (var (pos, opened) in changes)
            {
                if (opened)
                    changed |= Expand(gridUid, grid, comp, pos);
                else
                    changed |= Shrink(gridUid, grid, comp, pos);
            }

            if (changed)
                Dirty(gridUid, comp);
        }
        _pendingChanges.Clear();

        if (_dirtyGrids.Count == 0)
            return;

        _rebuildBuffer.Clear();
        _rebuildBuffer.AddRange(_dirtyGrids);

        var rebuilt = 0;
        foreach (var gridUid in _rebuildBuffer)
        {
            if (rebuilt >= MaxRebuildsPerUpdate)
                break;
            _dirtyGrids.Remove(gridUid);
            if (!_gridQuery.TryGetComponent(gridUid, out var grid))
                continue;
            Rebuild(gridUid, grid);
            rebuilt++;
        }
    }

    private bool Expand(EntityUid gridUid, MapGridComponent grid, WFExposureComponent comp, Vector2i pos)
    {
        var tile = _mapSystem.GetTileRef(gridUid, grid, pos);

        if (!tile.Tile.IsEmpty)
        {
            var connected = false;
            for (var i = 0; i < Cardinals.Length; i++)
            {
                var neighbor = pos + Cardinals[i];
                if (comp.Exposed.Contains(neighbor) || _mapSystem.GetTileRef(gridUid, grid, neighbor).Tile.IsEmpty)
                {
                    connected = true;
                    break;
                }
            }

            if (!connected)
                return false;
        }

        _bfsQueue.Clear();
        _bfsVisited.Clear();

        if (tile.Tile.IsEmpty)
        {
            for (var i = 0; i < Cardinals.Length; i++)
            {
                var neighbor = pos + Cardinals[i];
                var neighborTile = _mapSystem.GetTileRef(gridUid, grid, neighbor);
                if (neighborTile.Tile.IsEmpty || IsBlocked(gridUid, grid, neighbor))
                    continue;
                if (_bfsVisited.Add(neighbor))
                    _bfsQueue.Enqueue(neighbor);
            }
        }
        else if (!IsBlocked(gridUid, grid, pos))
        {
            _bfsQueue.Enqueue(pos);
            _bfsVisited.Add(pos);
        }

        var changed = false;
        while (_bfsQueue.TryDequeue(out var current))
        {
            if (comp.Exposed.Add(current))
            {
                changed = true;

                if (_baselined.Contains(gridUid))
                {
                    _builtFromEmpty.TryGetValue(gridUid, out var built);
                    if (built == null || !built.Contains(current))
                        comp.Rooved.Add(current);
                }
            }

            for (var i = 0; i < Cardinals.Length; i++)
            {
                var next = current + Cardinals[i];
                if (!_bfsVisited.Add(next))
                    continue;
                if (comp.Exposed.Contains(next))
                    continue;
                var nextTile = _mapSystem.GetTileRef(gridUid, grid, next);
                if (nextTile.Tile.IsEmpty)
                    continue;
                if (IsBlocked(gridUid, grid, next))
                    continue;
                _bfsQueue.Enqueue(next);
            }
        }

        return changed;
    }

    private bool Shrink(EntityUid gridUid, MapGridComponent grid, WFExposureComponent comp, Vector2i pos)
    {
        if (!comp.Exposed.Remove(pos))
            return false;

        var changed = true;

        for (var i = 0; i < Cardinals.Length; i++)
        {
            var neighbor = pos + Cardinals[i];
            if (!comp.Exposed.Contains(neighbor))
                continue;

            if (CanReachEdge(gridUid, grid, comp, neighbor, pos))
                continue;

            RemoveRegion(comp, neighbor, pos);
        }

        return changed;
    }

    private bool CanReachEdge(EntityUid gridUid, MapGridComponent grid, WFExposureComponent comp, Vector2i start, Vector2i avoid)
    {
        _bfsQueue.Clear();
        _bfsVisited.Clear();
        _bfsQueue.Enqueue(start);
        _bfsVisited.Add(start);
        _bfsVisited.Add(avoid);

        while (_bfsQueue.TryDequeue(out var current))
        {
            for (var i = 0; i < Cardinals.Length; i++)
            {
                var next = current + Cardinals[i];
                if (_mapSystem.GetTileRef(gridUid, grid, next).Tile.IsEmpty)
                    return true;

                if (!_bfsVisited.Add(next))
                    continue;
                if (!comp.Exposed.Contains(next))
                    continue;
                _bfsQueue.Enqueue(next);
            }
        }

        return false;
    }

    private void RemoveRegion(WFExposureComponent comp, Vector2i start, Vector2i avoid)
    {
        _bfsQueue.Clear();
        _bfsVisited.Clear();
        _bfsQueue.Enqueue(start);
        _bfsVisited.Add(start);
        _bfsVisited.Add(avoid);

        while (_bfsQueue.TryDequeue(out var current))
        {
            comp.Exposed.Remove(current);

            for (var i = 0; i < Cardinals.Length; i++)
            {
                var next = current + Cardinals[i];
                if (!_bfsVisited.Add(next))
                    continue;
                if (!comp.Exposed.Contains(next))
                    continue;
                _bfsQueue.Enqueue(next);
            }
        }
    }

    private bool AnyWeatherActive()
    {
        var query = EntityQueryEnumerator<WeatherComponent>();
        while (query.MoveNext(out _, out var weather))
        {
            if (weather.Weather.Count > 0)
                return true;
        }
        return false;
    }

    private void MarkWeatherGridsDirty()
    {
        var query = EntityQueryEnumerator<WeatherComponent, TransformComponent>();
        while (query.MoveNext(out _, out var weather, out var xform))
        {
            if (weather.Weather.Count == 0)
                continue;
            foreach (var grid in _mapManager.GetAllGrids(xform.MapID))
            {
                if (MetaData(grid.Owner).EntityPaused)
                    continue;
                _dirtyGrids.Add(grid.Owner);
            }
        }
    }

    private void Rebuild(EntityUid gridUid, MapGridComponent grid)
    {
        var comp = EnsureComp<WFExposureComponent>(gridUid);

        _oldExposed.Clear();
        _oldExposed.UnionWith(comp.Exposed);

        comp.Exposed.Clear();
        _bfsQueue.Clear();
        _bfsVisited.Clear();

        foreach (var tileRef in _mapSystem.GetAllTiles(gridUid, grid))
        {
            var pos = tileRef.GridIndices;
            if (IsBlocked(gridUid, grid, pos))
                continue;
            for (var i = 0; i < Cardinals.Length; i++)
            {
                var neighbour = _mapSystem.GetTileRef(gridUid, grid, pos + Cardinals[i]);
                if (!neighbour.Tile.IsEmpty)
                    continue;
                _bfsVisited.Add(pos);
                _bfsQueue.Enqueue(pos);
                break;
            }
        }

        while (_bfsQueue.TryDequeue(out var pos))
        {
            comp.Exposed.Add(pos);
            for (var i = 0; i < Cardinals.Length; i++)
            {
                var next = pos + Cardinals[i];
                if (!_bfsVisited.Add(next))
                    continue;
                var tile = _mapSystem.GetTileRef(gridUid, grid, next);
                if (tile.Tile.IsEmpty)
                    continue;
                if (IsBlocked(gridUid, grid, next))
                    continue;
                _bfsQueue.Enqueue(next);
            }
        }

        var oldRoovedCount = comp.Rooved.Count;

        RecordBreaches(gridUid, comp);

        if (!comp.Exposed.SetEquals(_oldExposed) || comp.Rooved.Count != oldRoovedCount)
            Dirty(gridUid, comp);
    }

    private void RecordBreaches(EntityUid gridUid, WFExposureComponent comp)
    {
        _builtFromEmpty.TryGetValue(gridUid, out var built);

        if (_baselined.Add(gridUid))
        {
            built?.Clear();
            return;
        }

        foreach (var pos in comp.Exposed)
        {
            if (_oldExposed.Contains(pos))
                continue;
            if (built != null && built.Contains(pos))
                continue;
            comp.Rooved.Add(pos);
        }

        built?.Clear();
    }

    private void ClearAll()
    {
        _dirtyGrids.Clear();
        _rebuildBuffer.Clear();
        _bfsQueue.Clear();
        _bfsVisited.Clear();
        _oldExposed.Clear();
        _baselined.Clear();
        _builtFromEmpty.Clear();
        _pendingChanges.Clear();

        var query = AllEntityQuery<WFExposureComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            RemCompDeferred<WFExposureComponent>(uid);
        }
    }

    private bool IsBlocked(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos);
        while (anchored.MoveNext(out var ent))
        {
            if (_blockQuery.HasComponent(ent.Value))
                return true;
        }
        return false;
    }
}
