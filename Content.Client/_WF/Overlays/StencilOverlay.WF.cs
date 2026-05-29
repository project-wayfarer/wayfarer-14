using System.Numerics;
using Content.Shared.Light.Components;
using Content.Shared.Tag;
using Content.Shared.Weather;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlay
{
    // The other dependencies on this overlay are filled in automatically, but an entity system
    // like TagSystem cannot be, so it is fetched by hand the first time it is needed.
    private TagSystem? _tagSystem;
    private TagSystem Tag => _tagSystem ??= _entManager.System<TagSystem>();

    private static readonly ProtoId<TagPrototype> DiagonalTag = "Diagonal";

    private static readonly Vector2i[] Cardinals =
        { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    // Three corners of the covered half-tile.
    private readonly Vector2[] _diagonalVerts = new Vector2[3];

    private void DrawWeather(in OverlayDrawArgs args, CachedResources res, WeatherPrototype weatherProto, float alpha, Matrix3x2 invMatrix)
    {
        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldAABB = args.WorldAABB;
        var worldBounds = args.WorldBounds;
        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;

        // Cut out the irrelevant bits via stencil
        // This is why we don't just use parallax; we might want specific tiles to get drawn over
        // particularly for planet maps or stations.
        worldHandle.RenderInRenderTarget(res.Blep!, () =>
        {
            var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
            _grids.Clear();

            // idk if this is safe to cache in a field and clear sloth help
            _mapManager.FindGridsIntersecting(mapId, worldAABB, ref _grids);

            foreach (var grid in _grids)
            {
                var matrix = _transform.GetWorldMatrix(grid, xformQuery);
                var matty = Matrix3x2.Multiply(matrix, invMatrix);
                worldHandle.SetTransform(matty);
                _entManager.TryGetComponent(grid.Owner, out RoofComponent? roofComp);

                // ignoreEmpty: false so diagonal walls on empty-space tiles still get their half-tile mask drawn.
                foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid, worldAABB, ignoreEmpty: false))
                {
                    if (HandleStencilTile(grid, tile, worldHandle, roofComp, weatherProto))
                        continue;

                    var gridTile = new Box2(tile.GridIndices * grid.Comp.TileSize,
                        (tile.GridIndices + Vector2i.One) * grid.Comp.TileSize);

                    worldHandle.DrawRect(gridTile, Color.White);
                }
            }

        }, Color.Transparent);

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_protoManager.Index(StencilMask).Instance());
        worldHandle.DrawTextureRect(res.Blep!.Texture, worldBounds);
        var curTime = _timing.RealTime;
        var sprite = _sprite.GetFrame(weatherProto.Sprite, curTime);

        // Draw the rain
        worldHandle.UseShader(_protoManager.Index(StencilDraw).Instance());
        _parallax.DrawParallax(worldHandle, worldAABB, sprite, curTime, position, Vector2.Zero, modulate: (weatherProto.Color ?? Color.White).WithAlpha(alpha));

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(null);
    }

    /// <summary>
    /// Decides whether to skip the full-tile mask for this tile, and draws the half-tile
    /// mask for any diagonal wall along the way. Returns true to skip when weather already
    /// reaches the tile, or when a diagonal on the edge of a grid should let weather through its open side.
    /// </summary>
    private bool HandleStencilTile(Entity<MapGridComponent> grid, TileRef tile, DrawingHandleWorld worldHandle, RoofComponent? roofComp, WeatherPrototype proto)
    {
        var canAffect = _weather.CanWeatherAffect(grid.Owner, grid, tile, proto, roofComp);
        var (hasDiagonals, hasFullEntity) = DrawDiagonalsForTile(grid, tile, worldHandle);

        if (canAffect)
            return true;

        if (hasDiagonals && !hasFullEntity && IsTileOrNeighborExposed(grid, tile, roofComp, proto))
            return true;

        return false;
    }

    /// <summary>
    /// Masks the covered half of any diagonal wall on the tile. Returns whether any diagonal
    /// was drawn and whether a non-diagonal wall is also on the tile.
    /// </summary>
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

            if (!Tag.HasTag(ent.Value, DiagonalTag))
            {
                hasFullEntity = true;
                continue;
            }

            hasDiagonals = true;

            // Mask the wall's half of the tile. Weather draws into the open half.
            if (!_entManager.TryGetComponent(ent.Value, out TransformComponent? entXform))
                continue;

            var rot = entXform.LocalRotation.GetCardinalDir();

            var sw = origin;
            var se = origin + new Vector2(size, 0);
            var ne = origin + new Vector2(size, size);
            var nw = origin + new Vector2(0, size);

            // GetCardinalDir only ever returns one of the four cardinals, so no default.
            switch (rot)
            {
                case Direction.South:
                    _diagonalVerts[0] = sw; _diagonalVerts[1] = se; _diagonalVerts[2] = ne;
                    break;
                case Direction.East:
                    _diagonalVerts[0] = se; _diagonalVerts[1] = ne; _diagonalVerts[2] = nw;
                    break;
                case Direction.North:
                    _diagonalVerts[0] = ne; _diagonalVerts[1] = nw; _diagonalVerts[2] = sw;
                    break;
                case Direction.West:
                    _diagonalVerts[0] = nw; _diagonalVerts[1] = sw; _diagonalVerts[2] = se;
                    break;
            }

            worldHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _diagonalVerts, Color.White);
        }

        return (hasDiagonals, hasFullEntity);
    }

    /// <summary>
    /// Returns true when weather can reach the tile or any of its four cardinal neighbours.
    /// Lets the caller tell a diagonal on the edge of a grid (next to lattice or empty space) apart
    /// from a decorative interior one.
    /// </summary>
    private bool IsTileOrNeighborExposed(Entity<MapGridComponent> grid, TileRef tile, RoofComponent? roofComp, WeatherPrototype proto)
    {
        if (_weather.CanWeatherAffect(grid.Owner, grid, tile, proto, roofComp))
            return true;

        var indices = tile.GridIndices;
        foreach (var off in Cardinals)
        {
            var neighbour = _map.GetTileRef(grid.Owner, grid, indices + off);
            if (_weather.CanWeatherAffect(grid.Owner, grid, neighbour, proto, roofComp))
                return true;
        }
        return false;
    }
}
