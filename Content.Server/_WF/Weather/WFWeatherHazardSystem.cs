using Content.Server.Atmos.EntitySystems;
using Content.Shared._WF.Weather;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Light.Components;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._WF.Weather;

// Hurts players standing where the weather can reach them, once a second.
public sealed class WFWeatherHazardSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedWFWeatherSystem _weather = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private EntityQuery<MapGridComponent> _gridQuery;

    private TimeSpan _nextUpdate;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;
        _nextUpdate = now + UpdateInterval;

        var weatherQuery = EntityQueryEnumerator<WFWeatherComponent, TransformComponent>();
        while (weatherQuery.MoveNext(out _, out var weatherComp, out var mapXform))
        {
            foreach (var (protoId, _) in weatherComp.Weather)
            {
                if (!_protoMan.TryIndex<WeatherPrototype>(protoId, out var proto) || proto.Damage == null)
                    continue;

                ApplyHazard(mapXform.MapID, proto);
            }
        }
    }

    private void ApplyHazard(MapId mapId, WeatherPrototype proto)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } uid)
                continue;

            var xform = Transform(uid);
            if (xform.MapID != mapId
                || xform.GridUid is not { } gridUid
                || !_gridQuery.TryGetComponent(gridUid, out var grid)
                || !_mapSystem.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tile)
                || !IsTileAffected(proto, _weather.ResolveWeatherGrid((gridUid, grid)), tile))
                continue;

            // Weather damage does not cancel surgery, construction or anything else a player is part way through.
            _damageable.TryChangeDamage(uid, proto.Damage!, interruptsDoAfters: false);
        }
    }

    private bool IsTileAffected(WeatherPrototype proto,
        Entity<MapGridComponent, WFExposureComponent?, RoofComponent?> grid, TileRef tile)
    {
        if (!_weather.CanWeatherAffect(grid, tile, proto))
            return false;

        // Permeating weather does not hurt anyone on a tile that still holds pressure.
        if (proto.ShelterType == WeatherShelter.Particulate)
            return true;

        // Passing the map would answer with the air outside for any tile the grid does not track.
        var mixture = _atmos.GetTileMixture(grid.Owner, map: null, tile.GridIndices);
        return mixture == null || mixture.Pressure < Atmospherics.WarningLowPressure;
    }
}
