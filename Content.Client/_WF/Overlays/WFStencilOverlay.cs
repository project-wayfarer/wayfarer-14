using System.Numerics;
using Content.Client.Graphics;
using Content.Client.Parallax;
using Content.Client._WF.Weather;
using Content.Shared._WF.Weather;
using Content.Shared.Light.Components;
using Content.Shared.Tag;
using Content.Shared.Weather;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using static Content.Shared._WF.Weather.WFExposureComponent;

namespace Content.Client._WF.Overlays;

public sealed class WFStencilOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> StencilMask = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilDraw = "StencilDraw";
    private static readonly ProtoId<TagPrototype> DiagonalTag = "Diagonal";

    private static readonly WeatherShelter[] ShelterKinds =
        { WeatherShelter.Particulate, WeatherShelter.Permeating };

    private const int MaxWallVerts = 1200;

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private readonly ParallaxSystem _parallax;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly TagSystem _tag;
    private readonly WFWeatherSystem _weather;

    // Passed by ref to the grid search, so it cannot be readonly.
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<Vector2> _wallHalfVerts = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly OverlayResourceCache<CachedResources> _resources = new();

    public WFStencilOverlay(
        ParallaxSystem parallax,
        SharedTransformSystem transform,
        SharedMapSystem map,
        SpriteSystem sprite,
        TagSystem tag,
        WFWeatherSystem weather)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _parallax = parallax;
        _map = map;
        _sprite = sprite;
        _tag = tag;
        _transform = transform;
        _weather = weather;
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var mapUid = _map.GetMapOrInvalid(args.MapId);

        if (!_entManager.TryGetComponent<WFWeatherComponent>(mapUid, out var comp) || comp.Weather.Count == 0)
            return;

        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

        if (res.StencilTexture?.Texture.Size != args.Viewport.Size)
        {
            res.StencilTexture?.Dispose();
            res.StencilTexture = _clyde.CreateRenderTarget(args.Viewport.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "wf-weather-stencil");
        }

        var invMatrix = args.Viewport.GetWorldToLocalMatrix();
        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldAABB = args.WorldAABB;

        // What gets covered depends only on what stops the weather, so two weathers stopped by the same thing are drawn together.
        foreach (var shelter in ShelterKinds)
        {
            var covered = false;

            foreach (var (protoId, weather) in comp.Weather)
            {
                if (!_protoManager.TryIndex(protoId, out var proto) || proto.ShelterType != shelter)
                    continue;

                if (!covered)
                {
                    var first = proto;
                    worldHandle.RenderInRenderTarget(res.StencilTexture!,
                        () => CoverSheltered(worldHandle, mapId, worldAABB, invMatrix, first), Color.Transparent);
                    covered = true;
                }

                DrawWeather(args, res, proto, _weather.GetPercent(weather, mapUid));
            }
        }

        worldHandle.UseShader(null);
        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();

        base.DisposeBehavior();
    }

    /// <summary>
    /// Frees the screen-sized buffers, which are made again on the next draw that needs them.
    /// </summary>
    public void ReleaseBuffers()
    {
        _resources.Dispose();
    }

    private void DrawWeather(in OverlayDrawArgs args, CachedResources res, WeatherPrototype proto, float alpha)
    {
        var worldHandle = args.WorldHandle;

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_protoManager.Index(StencilMask).Instance());
        worldHandle.DrawTextureRect(res.StencilTexture!.Texture, args.WorldBounds);
        var curTime = _timing.RealTime;
        var sprite = _sprite.GetFrame(proto.Sprite, curTime);

        worldHandle.UseShader(_protoManager.Index(StencilDraw).Instance());
        _parallax.DrawParallax(worldHandle, args.WorldAABB, sprite, curTime, args.Viewport.Eye?.Position.Position ?? Vector2.Zero, Vector2.Zero, modulate: (proto.Color ?? Color.White).WithAlpha(alpha));

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(null);
    }

    // Covers every tile that cannot have weather, which is what the weather is then cut out of.
    private void CoverSheltered(DrawingHandleWorld worldHandle, MapId mapId, Box2 worldAABB, Matrix3x2 invMatrix,
        WeatherPrototype proto)
    {
        _grids.Clear();
        _mapManager.FindGridsIntersecting(mapId, worldAABB, ref _grids);

        foreach (var grid in _grids)
        {
            worldHandle.SetTransform(Matrix3x2.Multiply(_transform.GetWorldMatrix(grid.Owner), invMatrix));
            var weatherGrid = _weather.ResolveWeatherGrid(grid);

            // Cut down to where this grid has tiles, or a small ship costs a look at every tile on screen.
            var bounds = _transform.GetInvWorldMatrix(grid.Owner).TransformBox(worldAABB);

            // A grid that is also the map never keeps its bounds up to date, so cutting it down would leave no weather drawn.
            if (!_entManager.HasComponent<MapComponent>(grid.Owner))
                bounds = grid.Comp.LocalAABB.Intersect(bounds);

            // Empty tiles are included so a diagonal wall standing on an empty tile still covers its own half.
            var tiles = _map.GetLocalTilesEnumerator(grid.Owner, grid, bounds, ignoreEmpty: false);
            while (tiles.MoveNext(out var tile))
            {
                var reaches = _weather.CanWeatherAffect(weatherGrid, tile, proto);

                // A tile that can have weather cannot have a wall on it, so the wall check can be skipped.
                if (reaches && !tile.Tile.IsEmpty)
                    continue;

                // A diagonal wall covers half its tile, so away from weather the whole tile is covered or a half-tile hole shows.
                var considerDiagonals = tile.Tile.IsEmpty || IsNeighborReached(weatherGrid, tile, proto);

                if (considerDiagonals)
                {
                    var (hasDiagonals, hasFullEntity) = DrawDiagonalsForTile(grid, tile, worldHandle);

                    if (reaches || hasDiagonals && !hasFullEntity)
                        continue;
                }

                var gridTile = new Box2(tile.GridIndices * grid.Comp.TileSize,
                    (tile.GridIndices + Vector2i.One) * grid.Comp.TileSize);

                worldHandle.DrawRect(gridTile, Color.White);
            }

            FlushWallHalves(worldHandle);
        }
    }

    private void FlushWallHalves(DrawingHandleWorld worldHandle)
    {
        if (_wallHalfVerts.Count == 0)
            return;

        worldHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _wallHalfVerts, Color.White);
        _wallHalfVerts.Clear();
    }

    private (bool hasDiagonals, bool hasFullEntity) DrawDiagonalsForTile(Entity<MapGridComponent> grid, TileRef tile, DrawingHandleWorld worldHandle)
    {
        var hasDiagonals = false;
        var hasFullEntity = false;

        var origin = (Vector2) tile.GridIndices * grid.Comp.TileSize;
        var size = grid.Comp.TileSize;

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid.Owner, grid, tile.GridIndices);
        while (anchored.MoveNext(out var ent))
        {
            if (!_entManager.HasComponent<BlockWeatherComponent>(ent.Value))
                continue;

            if (!_tag.HasTag(ent.Value, DiagonalTag))
            {
                hasFullEntity = true;
                continue;
            }

            hasDiagonals = true;

            if (!_entManager.TryGetComponent(ent.Value, out TransformComponent? entXform))
                continue;

            var rot = entXform.LocalRotation.GetCardinalDir();

            var sw = origin;
            var se = origin + new Vector2(size, 0);
            var ne = origin + new Vector2(size, size);
            var nw = origin + new Vector2(0, size);

            // The rotation is always one of the four cardinals, so there is no default.
            switch (rot)
            {
                case Direction.South: _wallHalfVerts.Add(sw); _wallHalfVerts.Add(se); _wallHalfVerts.Add(ne); break;
                case Direction.East: _wallHalfVerts.Add(se); _wallHalfVerts.Add(ne); _wallHalfVerts.Add(nw); break;
                case Direction.North: _wallHalfVerts.Add(ne); _wallHalfVerts.Add(nw); _wallHalfVerts.Add(sw); break;
                case Direction.West: _wallHalfVerts.Add(nw); _wallHalfVerts.Add(sw); _wallHalfVerts.Add(se); break;
            }

            if (_wallHalfVerts.Count >= MaxWallVerts)
                FlushWallHalves(worldHandle);
        }

        return (hasDiagonals, hasFullEntity);
    }

    private bool IsNeighborReached(Entity<MapGridComponent, WFExposureComponent?, RoofComponent?> grid, TileRef tile, WeatherPrototype proto)
    {
        var indices = tile.GridIndices;
        foreach (var off in Cardinals)
        {
            var neighbor = _map.GetTileRef(grid.Owner, grid.Comp1, indices + off);
            if (_weather.CanWeatherAffect(grid, neighbor, proto))
                return true;
        }
        return false;
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? StencilTexture;

        public void Dispose()
        {
            StencilTexture?.Dispose();
        }
    }
}
