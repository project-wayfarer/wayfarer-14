using Content.Shared.Light.Components;
using Content.Shared.Weather;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._WF.Weather;

public abstract class SharedWFWeatherSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IPrototypeManager ProtoMan = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WFWeatherComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    private void OnUnpaused(Entity<WFWeatherComponent> ent, ref EntityUnpausedEvent args)
    {
        foreach (var weather in ent.Comp.Weather.Values)
        {
            weather.StartTime += args.PausedTime;

            if (weather.EndTime != null)
                weather.EndTime = weather.EndTime.Value + args.PausedTime;
        }
    }

    public Entity<MapGridComponent, WFExposureComponent?, RoofComponent?> ResolveWeatherGrid(Entity<MapGridComponent> grid)
    {
        TryComp(grid.Owner, out WFExposureComponent? exposure);
        TryComp(grid.Owner, out RoofComponent? roof);
        return (grid.Owner, grid.Comp, exposure, roof);
    }

    /// <summary>
    /// Whether this weather can be on the tile. Smashing a window lets permeating weather in but not
    /// particulate, because the roof is still there.
    /// </summary>
    public bool CanWeatherAffect(Entity<MapGridComponent, WFExposureComponent?, RoofComponent?> grid,
        TileRef tileRef, WeatherPrototype proto)
    {
        if (tileRef.Tile.IsEmpty)
            return true;

        // Everything that roofs a tile also stops weather, so the only roof left to find is what is on the map file.
        if (grid.Comp3 is { Data.Count: > 0 } roof && HasRoofBit(roof, tileRef.GridIndices))
            return false;

        return grid.Comp2?.WeatherReaches(tileRef.GridIndices, proto.ShelterType) ?? false;
    }

    private static bool HasRoofBit(RoofComponent roof, Vector2i pos)
    {
        var chunk = SharedMapSystem.GetChunkIndices(pos, RoofComponent.ChunkSize);
        if (!roof.Data.TryGetValue(chunk, out var mask))
            return false;

        var relative = SharedMapSystem.GetChunkRelative(pos, RoofComponent.ChunkSize);
        return (mask & SharedMapSystem.ToBitmask(relative, (byte) RoofComponent.ChunkSize)) != 0;
    }

    public float GetPercent(WFWeatherData data, EntityUid mapUid)
    {
        var pauseTime = _metadata.GetPauseTime(mapUid);
        var elapsed = Timing.CurTime - (data.StartTime + pauseTime);
        var remaining = data.Duration - elapsed;

        if (remaining < WFWeatherComponent.ShutdownTime)
            return (float) (remaining / WFWeatherComponent.ShutdownTime);

        if (elapsed < WFWeatherComponent.StartupTime)
            return (float) (elapsed / WFWeatherComponent.StartupTime);

        return 1f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var curTime = Timing.CurTime;

        var query = EntityQueryEnumerator<WFWeatherComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var (protoId, weather) in comp.Weather)
            {
                var endTime = weather.EndTime;

                if (endTime != null && endTime < curTime)
                {
                    EndWeather(uid, comp, protoId);
                    continue;
                }

                if (!ProtoMan.TryIndex(protoId, out var proto))
                {
                    Log.Error($"No weather prototype named {protoId}, ending it.");
                    EndWeather(uid, comp, protoId);
                    continue;
                }

                if (endTime != null && endTime - curTime < WFWeatherComponent.ShutdownTime)
                    SetState(uid, WFWeatherState.Ending, comp, weather, proto);
                else if (curTime - weather.StartTime < WFWeatherComponent.StartupTime)
                    SetState(uid, WFWeatherState.Starting, comp, weather, proto);

                Run(uid, weather, proto, frameTime);
            }
        }
    }

    /// <summary>
    /// Fades out everything already running on the map and starts the new weather, if there is one.
    /// </summary>
    public void SetWeather(MapId mapId, WeatherPrototype? proto, TimeSpan? endTime)
    {
        if (!_mapSystem.TryGetMap(mapId, out var mapUid))
            return;

        // Clearing weather on a map that has none would leave it carrying weather data for the rest of the round.
        if (!TryComp<WFWeatherComponent>(mapUid.Value, out var comp))
        {
            if (proto == null)
                return;

            comp = AddComp<WFWeatherComponent>(mapUid.Value);
        }

        foreach (var (runningId, weather) in comp.Weather)
        {
            if (proto != null && runningId == proto.ID)
            {
                weather.EndTime = endTime;
                if (weather.State == WFWeatherState.Ending)
                    weather.State = WFWeatherState.Running;

                Dirty(mapUid.Value, comp);
                continue;
            }

            // Clearing the weather with a time given ends it then, otherwise everything running fades out from now.
            var end = proto == null && endTime != null
                ? endTime.Value
                : Timing.CurTime + WFWeatherComponent.ShutdownTime;

            if (weather.EndTime == null || weather.EndTime > end)
            {
                weather.EndTime = end;
                Dirty(mapUid.Value, comp);
            }
        }

        if (proto != null)
            StartWeather(mapUid.Value, comp, proto, endTime);
    }

    protected virtual void Run(EntityUid uid, WFWeatherData weather, WeatherPrototype proto, float frameTime) { }

    protected void StartWeather(EntityUid uid, WFWeatherComponent comp, WeatherPrototype proto, TimeSpan? endTime)
    {
        if (comp.Weather.ContainsKey(proto.ID))
            return;

        comp.Weather.Add(proto.ID, new WFWeatherData
        {
            StartTime = Timing.CurTime,
            EndTime = endTime,
        });

        Dirty(uid, comp);
    }

    protected virtual void EndWeather(EntityUid uid, WFWeatherComponent comp, string protoId)
    {
        if (!comp.Weather.TryGetValue(protoId, out var data))
            return;

        _audio.Stop(data.Stream);
        data.Stream = null;
        comp.Weather.Remove(protoId);
        Dirty(uid, comp);
    }

    protected virtual bool SetState(EntityUid uid, WFWeatherState state, WFWeatherComponent comp, WFWeatherData weather, WeatherPrototype proto)
    {
        if (weather.State == state)
            return false;

        weather.State = state;
        Dirty(uid, comp);
        return true;
    }

    [Serializable, NetSerializable]
    protected sealed class WFWeatherComponentState(Dictionary<ProtoId<WeatherPrototype>, WFWeatherData> weather) : ComponentState
    {
        public Dictionary<ProtoId<WeatherPrototype>, WFWeatherData> Weather = weather;
    }
}
