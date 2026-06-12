 using Content.Shared._WF.Weather;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;

namespace Content.Server._WF.Weather;

// Tracks which tiles on each grid are open to weather. A tile is open when nothing walls it off
// from space. Recomputed when walls or floors change.
public sealed class WFWeatherExposureSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    private EntityQuery<BlockWeatherComponent> _blockQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    private readonly HashSet<EntityUid> _dirtyGrids = new();
    private readonly List<EntityUid> _rebuildBuffer = new();
    private readonly Queue<Vector2i> _bfsQueue = new();
    private readonly HashSet<Vector2i> _bfsVisited = new();

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

    private void OnGridInit(GridInitializeEvent ev) => _dirtyGrids.Add(ev.EntityUid);

    private void OnTileChanged(ref TileChangedEvent ev) => _dirtyGrids.Add(ev.Entity.Owner);

    private void OnBlockWeatherAnchor(Entity<BlockWeatherComponent> ent, ref AnchorStateChangedEvent args)
        => MarkOwningGridDirty(ent.Owner);

    // Walls drawn on a map at load time do not announce themselves the way a wall built in-game does.
    // Recheck which tiles are open when the map loads so these walls are counted too.
    private void OnBlockWeatherMapInit(Entity<BlockWeatherComponent> ent, ref MapInitEvent args)
        => MarkOwningGridDirty(ent.Owner);

    private void MarkOwningGridDirty(EntityUid owner)
    {
        var gridUid = Transform(owner).GridUid;
        if (gridUid != null)
            _dirtyGrids.Add(gridUid.Value);
    }

    public override void Update(float frameTime)
    {
        if (_dirtyGrids.Count == 0)
            return;

        _rebuildBuffer.Clear();
        _rebuildBuffer.AddRange(_dirtyGrids);
        _dirtyGrids.Clear();

        foreach (var gridUid in _rebuildBuffer)
        {
            if (!_gridQuery.TryGetComponent(gridUid, out var grid))
                continue;
            Rebuild(gridUid, grid);
        }
    }

    private void Rebuild(EntityUid gridUid, MapGridComponent grid)
    {
        var comp = EnsureComp<WFExposureComponent>(gridUid);
        comp.Exposed.Clear();
        _bfsQueue.Clear();
        _bfsVisited.Clear();

        // Start from every floor tile that has at least one open-space neighbour. These are the
        // tiles directly touching space.
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

        // Every tile reachable from the edge tiles without crossing a wall is added to Exposed.
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

        // Any tile not in Exposed is added to Rooved. Tiles never leave Rooved, so a room
        // stays a sheltered room for rain and snow even after a wall is broken.
        foreach (var tileRef in _mapSystem.GetAllTiles(gridUid, grid))
        {
            var pos = tileRef.GridIndices;
            if (comp.Exposed.Contains(pos))
                continue;
            comp.Rooved.Add(pos);
        }

        Dirty(gridUid, comp);
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
