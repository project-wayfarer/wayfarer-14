using System.Runtime.InteropServices;
using Content.Shared._WF.Weather;
using Content.Shared.Weather;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Shared._WF.Weather.WFExposureComponent;

namespace Content.Server._WF.Weather;

public sealed class WFWeatherExposureSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private const int MaxRecountsPerUpdate = 2;
    private const int PruneWindowSeconds = 300;
    private const int MaxSealedRegionTiles = 4096;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(30);

    private TimeSpan _nextUpdate;
    private TimeSpan _nextPrune;

    private EntityQuery<BlockWeatherComponent> _blockQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<WFExposureComponent> _exposureQuery;

    private readonly HashSet<EntityUid> _weatherMaps = new();
    private readonly Dictionary<EntityUid, List<ICommonSession>> _mapWatchers = new();

    private readonly Queue<Vector2i> _searchQueue = new();
    private readonly Dictionary<Vector2i, ulong> _visited = new();
    private readonly Dictionary<Vector2i, ulong> _reachesOutside = new();
    private readonly List<Vector2i> _regionScratch = new();
    private readonly Dictionary<Vector2i, WFExposureChunk> _countScratch = new();

    // The list of maps with weather is only refreshed once a second, so weather that has just started is not noticed straight away.
    private bool AnyWeatherRunning => _weatherMaps.Count > 0;

    // Without this the tiles kept for each grid are never thrown away once the last weather ends.
    private bool _hadWeather;

    public override void Initialize()
    {
        base.Initialize();

        _blockQuery = GetEntityQuery<BlockWeatherComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _exposureQuery = GetEntityQuery<WFExposureComponent>();

        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<BlockWeatherComponent, AnchorStateChangedEvent>(OnBlockWeatherAnchor);
        SubscribeLocalEvent<BlockWeatherComponent, MapInitEvent>(OnBlockWeatherMapInit);
        SubscribeLocalEvent<WFExposureComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<WFExposureComponent, ComponentGetStateAttemptEvent>(OnGetStateAttempt);

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        var query = AllEntityQuery<WFExposureComponent>();
        while (query.MoveNext(out _, out var comp))
            comp.DropCopy(args.Session);
    }

    // A grid being deleted still reports its tiles going away, so nothing here can assume it still has a position.
    private void OnTileChanged(ref TileChangedEvent ev)
    {
        if (!AnyWeatherRunning || !TryComp<WFExposureComponent>(ev.Entity.Owner, out var comp))
            return;

        foreach (var change in ev.Changes)
        {
            if (!change.EmptyChanged)
                continue;

            comp.Pending.Add((change.GridIndices,
                change.OldTile.IsEmpty ? WFTileChange.Created : WFTileChange.Removed));
        }
    }

    private void OnBlockWeatherAnchor(Entity<BlockWeatherComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!AnyWeatherRunning)
            return;

        QueueBlockerChange(args.Transform, args.Anchored ? WFTileChange.Blocked : WFTileChange.Unblocked);
    }

    private void OnBlockWeatherMapInit(Entity<BlockWeatherComponent> ent, ref MapInitEvent args)
    {
        if (!AnyWeatherRunning)
            return;

        if (Transform(ent.Owner) is { Anchored: true } xform)
            QueueBlockerChange(xform, WFTileChange.Blocked);
    }

    private void QueueBlockerChange(TransformComponent xform, WFTileChange change)
    {
        if (xform.GridUid is not { } gridUid
            || !TryComp<WFExposureComponent>(gridUid, out var comp)
            || !_gridQuery.TryGetComponent(gridUid, out var grid))
            return;

        comp.Pending.Add((_mapSystem.TileIndicesFor(gridUid, grid, xform.Coordinates), change));
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;
        _nextUpdate = now + UpdateInterval;

        _weatherMaps.Clear();

        // Paused maps are skipped below, so a map that pauses drops out of this list on its own.
        var weatherQuery = EntityQueryEnumerator<WFWeatherComponent>();
        while (weatherQuery.MoveNext(out var mapUid, out var weather))
        {
            if (weather.Weather.Count > 0)
                _weatherMaps.Add(mapUid);
        }

        if (!AnyWeatherRunning && !_hadWeather)
            return;

        _hadWeather = AnyWeatherRunning;

        var gridQuery = AllEntityQuery<MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out var gridUid, out _, out var xform))
        {
            if (xform.MapUid is { } mapUid && _weatherMaps.Contains(mapUid))
                EnsureComp<WFExposureComponent>(gridUid);
            else if (_exposureQuery.HasComponent(gridUid))
                RemCompDeferred<WFExposureComponent>(gridUid);
        }

        if (!AnyWeatherRunning)
        {
            // Otherwise the watcher lists hold on to players long after the weather has ended.
            _mapWatchers.Clear();
            return;
        }

        SendToPlayersMissingTiles();
        DrainPending();

        if (now >= _nextPrune)
        {
            _nextPrune = now + PruneInterval;
            PruneLogs();
        }

        Recount();
    }

    private void DrainPending()
    {
        var tick = _timing.CurTick;
        var query = AllEntityQuery<WFExposureComponent, MapGridComponent>();
        while (query.MoveNext(out var gridUid, out var comp, out var grid))
        {
            if (comp.Pending.Count == 0)
                continue;

            // A grid still waiting for its tiles to be worked out takes tile changes anyway, because roofs cannot be worked out later.
            var counting = !comp.Counted;
            var changed = false;

            _searchQueue.Clear();
            _visited.Clear();

            // Every tile that opened this second is searched together, or ground appearing on a planet would start thousands of searches.
            foreach (var (pos, change) in comp.Pending)
            {
                switch (change)
                {
                    case WFTileChange.Created when !_mapSystem.GetTileRef(gridUid, grid, pos).Tile.IsEmpty:
                        changed |= comp.SetOpenOverhead(pos, tick);
                        if (!counting)
                            SeedOpening(gridUid, grid, comp, pos);
                        break;

                    case WFTileChange.Removed when _mapSystem.GetTileRef(gridUid, grid, pos).Tile.IsEmpty:
                        // Without this a tile that has stopped existing can still have weather.
                        changed |= comp.Close(pos, tick);
                        if (!counting)
                            SeedNeighbors(gridUid, grid, pos);
                        break;

                    case WFTileChange.Unblocked when !counting && !_mapSystem.GetTileRef(gridUid, grid, pos).Tile.IsEmpty:
                        SeedOpening(gridUid, grid, comp, pos);
                        break;
                }
            }

            if (!counting)
            {
                changed |= RunOpening(gridUid, grid, comp, tick);

                foreach (var (pos, change) in comp.Pending)
                {
                    if (change != WFTileChange.Blocked)
                        continue;

                    changed |= SealAt(gridUid, grid, comp, pos, tick);
                }
            }

            comp.Pending.Clear();

            if (changed)
                Dirty(gridUid, comp);
        }
    }

    private void SeedOpening(EntityUid gridUid, MapGridComponent grid, WFExposureComponent comp, Vector2i pos)
    {
        if (IsBlocked(gridUid, grid, pos))
            return;

        for (var i = 0; i < Cardinals.Length; i++)
        {
            var neighbor = pos + Cardinals[i];
            if (!comp.IsExposed(neighbor) && !_mapSystem.GetTileRef(gridUid, grid, neighbor).Tile.IsEmpty)
                continue;

            if (SetBit(_visited, pos))
                _searchQueue.Enqueue(pos);
            return;
        }
    }

    private void SeedNeighbors(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        for (var i = 0; i < Cardinals.Length; i++)
        {
            var neighbor = pos + Cardinals[i];
            if (!_mapSystem.GetTileRef(gridUid, grid, neighbor).Tile.IsEmpty
                && !IsBlocked(gridUid, grid, neighbor)
                && SetBit(_visited, neighbor))
                _searchQueue.Enqueue(neighbor);
        }
    }

    private bool RunOpening(EntityUid gridUid, MapGridComponent grid, WFExposureComponent comp, GameTick tick)
    {
        var changed = false;

        while (_searchQueue.TryDequeue(out var current))
        {
            changed |= comp.SetOpenToOutside(current, tick);

            for (var i = 0; i < Cardinals.Length; i++)
            {
                var next = current + Cardinals[i];
                if (SetBit(_visited, next) && !comp.IsExposed(next)
                    && !_mapSystem.GetTileRef(gridUid, grid, next).Tile.IsEmpty && !IsBlocked(gridUid, grid, next))
                    _searchQueue.Enqueue(next);
            }
        }

        return changed;
    }

    private bool SealAt(EntityUid gridUid, MapGridComponent grid, WFExposureComponent comp, Vector2i pos, GameTick tick)
    {
        // Whatever was blocking this tile may already be gone, and sealing it then would shut weather out of a tile standing open.
        if (!IsBlocked(gridUid, grid, pos))
            return false;

        var wasOpen = comp.IsExposed(pos);
        var changed = comp.Seal(pos, tick);

        if (!wasOpen)
            return changed;

        _reachesOutside.Clear();

        for (var i = 0; i < Cardinals.Length; i++)
        {
            var neighbor = pos + Cardinals[i];
            if (!comp.IsExposed(neighbor) || HasBit(_reachesOutside, neighbor))
                continue;

            changed |= CloseRegionIfSealed(gridUid, grid, comp, neighbor, pos, tick);
        }

        return changed;
    }

    private bool CloseRegionIfSealed(EntityUid gridUid, MapGridComponent grid, WFExposureComponent comp,
        Vector2i start, Vector2i avoid, GameTick tick)
    {
        _searchQueue.Clear();
        _visited.Clear();
        _regionScratch.Clear();
        _searchQueue.Enqueue(start);
        SetBit(_visited, start);
        SetBit(_visited, avoid);
        _regionScratch.Add(start);

        var seen = 2;

        while (_searchQueue.TryDequeue(out var current))
        {
            // On a planet the search would cross everything loaded looking for an opening, so an area this large keeps its weather.
            if (seen > MaxSealedRegionTiles)
            {
                MarkReachesOutside();
                return false;
            }

            for (var i = 0; i < Cardinals.Length; i++)
            {
                var next = current + Cardinals[i];
                if (_mapSystem.GetTileRef(gridUid, grid, next).Tile.IsEmpty)
                {
                    MarkReachesOutside();
                    return false;
                }

                if (!SetBit(_visited, next))
                    continue;

                seen++;

                if (comp.IsExposed(next))
                {
                    _searchQueue.Enqueue(next);
                    _regionScratch.Add(next);
                }
            }
        }

        var changed = false;
        foreach (var tile in _regionScratch)
            changed |= comp.Close(tile, tick);

        return changed;
    }

    // The whole area the search covered keeps its weather, so the other sides of the tile do not need their own search.
    private void MarkReachesOutside()
    {
        foreach (var (chunk, mask) in _visited)
        {
            ref var reaches = ref CollectionsMarshal.GetValueRefOrAddDefault(_reachesOutside, chunk, out _);
            reaches |= mask;
        }
    }

    private void Recount()
    {
        var recounted = 0;
        var query = AllEntityQuery<WFExposureComponent, MapGridComponent>();
        while (recounted < MaxRecountsPerUpdate && query.MoveNext(out var gridUid, out var comp, out var grid))
        {
            if (comp.Counted)
                continue;

            // A paused grid has its tiles worked out once it starts again.
            if (Paused(gridUid))
                continue;

            RecountGrid(gridUid, grid, comp);
            recounted++;
        }
    }

    private void RecountGrid(EntityUid gridUid, MapGridComponent grid, WFExposureComponent comp)
    {
        _countScratch.Clear();
        _searchQueue.Clear();
        _visited.Clear();

        // Tiles with something on them are thrown out further down, so checking in this loop as well would look at every tile twice.
        var tiles = _mapSystem.GetAllTilesEnumerator(gridUid, grid);
        while (tiles.MoveNext(out var tileRef))
        {
            var pos = tileRef.Value.GridIndices;

            if (HasEmptyNeighbor(gridUid, grid, pos) && SetBit(_visited, pos))
                _searchQueue.Enqueue(pos);
        }

        while (_searchQueue.TryDequeue(out var pos))
        {
            if (IsBlocked(gridUid, grid, pos))
                continue;

            var (chunk, bit) = GetChunkBit(pos);
            ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_countScratch, chunk, out _);
            entry.OpenToOutside |= bit;

            for (var i = 0; i < Cardinals.Length; i++)
            {
                var next = pos + Cardinals[i];
                if (SetBit(_visited, next) && !_mapSystem.GetTileRef(gridUid, grid, next).Tile.IsEmpty)
                    _searchQueue.Enqueue(next);
            }
        }

        StoreCount(gridUid, comp);
    }

    private void StoreCount(EntityUid gridUid, WFExposureComponent comp)
    {
        var tick = _timing.CurTick;
        var changed = false;

        foreach (var (chunk, _) in comp.Chunks)
        {
            if (!_countScratch.ContainsKey(chunk))
                _countScratch[chunk] = default;
        }

        foreach (var (chunk, fresh) in _countScratch)
        {
            comp.Chunks.TryGetValue(chunk, out var old);

            // A roof cannot be seen from the tiles alone, so a tile only counts as rooved once it has been walled in.
            var overhead = comp.Counted
                ? old.OpenOverhead & ~(old.OpenToOutside & ~fresh.OpenToOutside)
                : fresh.OpenToOutside;

            if (old.OpenToOutside == fresh.OpenToOutside && old.OpenOverhead == overhead)
                continue;

            if (fresh.OpenToOutside == 0 && overhead == 0)
                comp.Chunks.Remove(chunk);
            else
                comp.Chunks[chunk] = new WFExposureChunk { OpenToOutside = fresh.OpenToOutside, OpenOverhead = overhead };

            comp.Stamp(chunk, tick);
            changed = true;
        }

        comp.Counted = true;

        if (changed)
            Dirty(gridUid, comp);
    }

    private void PruneLogs()
    {
        var curTick = _timing.CurTick;
        var window = (uint) (PruneWindowSeconds * _timing.TickRate);
        var before = curTick.Value > window ? new GameTick(curTick.Value - window) : GameTick.Zero;

        var query = AllEntityQuery<WFExposureComponent>();
        while (query.MoveNext(out _, out var comp))
            comp.PruneLog(before);
    }

    // Nothing tells the server when a player starts seeing a map, so anyone missing this grid's tiles has to be found by looking.
    private void SendToPlayersMissingTiles()
    {
        foreach (var (mapUid, watchers) in _mapWatchers)
        {
            if (_weatherMaps.Contains(mapUid))
                watchers.Clear();
            else
                _mapWatchers.Remove(mapUid);
        }

        foreach (var session in _player.Sessions)
        {
            foreach (var mapUid in _weatherMaps)
            {
                if (CanSeeMap(session, mapUid))
                    _mapWatchers.GetOrNew(mapUid).Add(session);
            }
        }

        var query = AllEntityQuery<WFExposureComponent, TransformComponent>();
        while (query.MoveNext(out var gridUid, out var comp, out var xform))
        {
            if (xform.MapUid is not { } mapUid || !_mapWatchers.TryGetValue(mapUid, out var watchers))
                continue;

            foreach (var session in watchers)
            {
                if (comp.HasCopy(session))
                    continue;

                Dirty(gridUid, comp);
                break;
            }
        }
    }

    private void OnGetState(Entity<WFExposureComponent> ent, ref ComponentGetState args)
    {
        var comp = ent.Comp;

        // With no player to send to there is no way to know what they already have, so send everything.
        if (args.Player is not { } player)
        {
            args.State = FullState(comp);
            return;
        }

        // Keep sending the copy until the player confirms it, or a lost packet leaves them nothing to update.
        var now = _timing.CurTick;
        var sentAt = comp.MarkCopySent(player, now);

        // A player seeing the grid for the first time, or too far behind, gets the full list of tiles.
        if (args.FromTick < sentAt || args.FromTick <= comp.CreationTick || args.FromTick <= comp.LastPrune)
        {
            args.State = FullState(comp);
            return;
        }

        var start = FirstRecordAtOrAfter(comp.Log, args.FromTick);
        var open = new Dictionary<Vector2i, ulong>(comp.Log.Count - start);
        var covered = new Dictionary<Vector2i, ulong>();

        for (var i = start; i < comp.Log.Count; i++)
            Encode(comp, comp.Log[i].Chunk, open, covered);

        args.State = new WFExposureDeltaState(open, covered);
    }

    private static WFExposureState FullState(WFExposureComponent comp)
    {
        var open = new Dictionary<Vector2i, ulong>(comp.Chunks.Count);
        var covered = new Dictionary<Vector2i, ulong>();

        foreach (var (chunk, entry) in comp.Chunks)
        {
            // A chunk where no tile can have weather answers no everywhere, so the client is never told about it.
            if (entry.OpenToOutside != 0)
                Encode(comp, chunk, open, covered);
        }

        return new WFExposureState(open, covered);
    }

    // A chunk that has gone gets a zero, which is how the client is told to drop it.
    private static void Encode(WFExposureComponent comp, Vector2i chunk,
        Dictionary<Vector2i, ulong> open, Dictionary<Vector2i, ulong> covered)
    {
        comp.Chunks.TryGetValue(chunk, out var entry);
        open[chunk] = entry.OpenToOutside;

        var mask = entry.OpenToOutside & ~entry.OpenOverhead;
        if (mask != 0)
            covered[chunk] = mask;
    }

    // Several changes can share a tick, and a plain search would pick any one of them instead of the first.
    private static int FirstRecordAtOrAfter(List<(GameTick Tick, Vector2i Chunk)> log, GameTick tick)
    {
        var low = 0;
        var high = log.Count;

        while (low < high)
        {
            var mid = (low + high) / 2;
            if (log[mid].Tick < tick)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    private void OnGetStateAttempt(Entity<WFExposureComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player is not { } player)
            return;

        if (_transform.GetMap(ent.Owner) is { } mapUid && CanSeeMap(player, mapUid))
            return;

        // Forgetting the player is what gets them everything again if they come back.
        ent.Comp.DropCopy(player);
        args.Cancelled = true;
    }

    // Checks every eye a player has, not just their body, so a view onto another map still gets weather.
    private bool CanSeeMap(ICommonSession player, EntityUid mapUid)
    {
        if (IsOnMap(player.AttachedEntity, mapUid))
            return true;

        foreach (var viewer in player.ViewSubscriptions)
        {
            if (IsOnMap(viewer, mapUid))
                return true;
        }

        return false;
    }

    private bool IsOnMap(EntityUid? uid, EntityUid mapUid)
        => uid is { } value && _transform.GetMap(value) == mapUid;

    private bool IsBlocked(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos);
        while (anchored.MoveNext(out var ent))
            if (_blockQuery.HasComponent(ent.Value))
                return true;
        return false;
    }

    private bool HasEmptyNeighbor(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        for (var i = 0; i < Cardinals.Length; i++)
        {
            if (_mapSystem.GetTileRef(gridUid, grid, pos + Cardinals[i]).Tile.IsEmpty)
                return true;
        }
        return false;
    }
}
