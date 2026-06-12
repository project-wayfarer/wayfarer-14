using Content.Server.Atmos.EntitySystems;
using Content.Server.NPC.HTN;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._WF.Weather;

// Hurts mobs standing where the weather can reach them. Each weather has its own damage
// rate set in YAML, default one second between hits.
public sealed class WFWeatherHazardSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;

    private EntityQuery<MapGridComponent> _gridQuery;

    // When each weather on each map is allowed to deal damage next.
    private readonly Dictionary<(EntityUid Map, string ProtoId), TimeSpan> _nextTick = new();

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var weatherQuery = EntityQueryEnumerator<WeatherComponent, TransformComponent>();
        while (weatherQuery.MoveNext(out var mapUid, out var weatherComp, out var mapXform))
        {
            if (weatherComp.Weather.Count == 0)
                continue;

            foreach (var (protoId, _) in weatherComp.Weather)
            {
                if (!_protoMan.TryIndex<WeatherPrototype>(protoId, out var proto))
                    continue;
                if (proto.Damage == null)
                    continue;

                var key = (mapUid, protoId.Id);
                if (_nextTick.TryGetValue(key, out var next) && now < next)
                    continue;
                _nextTick[key] = now + proto.DamageInterval;

                ApplyHazard(mapXform.MapID, proto);
            }
        }
    }

    private void ApplyHazard(MapId mapId, WeatherPrototype proto)
    {
        var mobQuery = EntityQueryEnumerator<MobStateComponent, DamageableComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (HasComp<HTNComponent>(uid))
                continue;

            if (xform.MapID != mapId)
                continue;
            if (xform.GridUid is not { } gridUid)
                continue;
            if (!_gridQuery.TryGetComponent(gridUid, out var grid))
                continue;
            if (!_mapSystem.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tile))
                continue;
            if (!IsTileAffected(proto, gridUid, grid, tile))
                continue;

            _damageable.TryChangeDamage(uid, proto.Damage!);
        }
    }

    // Gas and radiation are also stopped by a sealed pressurised tile. The check reads only the
    // tile's own air, not the space around the grid, or a sealed interior would read as vacuum
    // and hurt the players inside.
    private bool IsTileAffected(WeatherPrototype proto, EntityUid gridUid, MapGridComponent grid, TileRef tile)
    {
        if (!_weather.CanWeatherAffect(gridUid, grid, tile, proto))
            return false;
        if (proto.Particulate != null)
            return true;

        var mixture = _atmos.GetTileMixture(gridUid, null, tile.GridIndices);
        if (mixture == null)
            return true;
        return mixture.Pressure < Atmospherics.WarningLowPressure;
    }
}
