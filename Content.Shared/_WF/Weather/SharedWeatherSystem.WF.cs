using Content.Shared._WF.Weather;
using Content.Shared.Light.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.Weather;

public abstract partial class SharedWeatherSystem
{
    // True when weather can reach this tile. Space always counts. Tiles under a roof do not.
    // Otherwise checks the grid's Exposed set.
    public bool CanWeatherAffect(EntityUid uid, MapGridComponent grid, TileRef tileRef, RoofComponent? roofComp = null)
    {
        if (tileRef.Tile.IsEmpty)
            return true;

        if (Resolve(uid, ref roofComp, false) && _roof.IsRooved((uid, grid, roofComp), tileRef.GridIndices))
            return false;

        return TryComp<WFExposureComponent>(uid, out var exposure)
            && exposure.Exposed.Contains(tileRef.GridIndices);
    }

    // Rain and snow are stopped by walls and roofs both. Smashing a window does not let them
    // in, because the roof is still there. Gas and radiation only need an opening to a tile,
    // so a smashed window lets them in.
    public bool CanWeatherAffect(EntityUid uid, MapGridComponent grid, TileRef tileRef, WeatherPrototype proto, RoofComponent? roofComp = null)
    {
        if (tileRef.Tile.IsEmpty)
            return true;

        if (Resolve(uid, ref roofComp, false) && _roof.IsRooved((uid, grid, roofComp), tileRef.GridIndices))
            return false;

        if (!TryComp<WFExposureComponent>(uid, out var exposure))
            return false;

        if (!exposure.Exposed.Contains(tileRef.GridIndices))
            return false;

        return proto.Particulate == null || !exposure.Rooved.Contains(tileRef.GridIndices);
    }
}
