using Content.Client.Parallax;
using Content.Client._WF.Overlays;
using Content.Shared._WF.Weather;
using Content.Shared.Light.Components;
using Content.Shared.Tag;
using Content.Shared.Weather;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using AudioComponent = Robust.Shared.Audio.Components.AudioComponent;

namespace Content.Client._WF.Weather;

public sealed class WFWeatherSystem : SharedWFWeatherSystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ParallaxSystem _parallax = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    // Muffling levels picked by how many tiles the player is from the nearest tile with weather on it.
    private const float OcclusionSilent = 3f;
    private const float OcclusionInterior = 1.5f;
    private const float OcclusionBoundary = 0.7f;

    // The muffled-against-the-wall level reaches this many tiles in from the weather.
    private const int BoundaryDepth = 2;

    // Weather farther than this from the player is silent.
    private const int MaxSearchDepth = 16;

    // How fast the volume catches up as the player moves between sheltered and open tiles.
    private const float OcclusionFadeRate = 0.5f;

    private const float OcclusionInterval = 0.25f;

    private WFStencilOverlay _stencil = default!;

    private bool _anyWeather;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WFWeatherComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<WFWeatherComponent, ComponentShutdown>(OnShutdown);

        _stencil = new WFStencilOverlay(_parallax, _transform, _mapSystem, _sprite, _tag, this);
        _overlay.AddOverlay(_stencil);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<WFStencilOverlay>();

        // Taking the overlay off the manager does not free the screen-sized buffers it is holding.
        _stencil.Dispose();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var any = AnyWeatherRunning();
        if (any == _anyWeather)
            return;

        _anyWeather = any;

        if (!any)
            _stencil.ReleaseBuffers();
    }

    private bool AnyWeatherRunning()
    {
        var query = EntityQueryEnumerator<WFWeatherComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.Weather.Count > 0)
                return true;
        }

        return false;
    }

    private void OnHandleState(Entity<WFWeatherComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not WFWeatherComponentState state)
            return;

        var comp = ent.Comp;

        foreach (var (protoId, weather) in comp.Weather)
        {
            if (!state.Weather.TryGetValue(protoId, out var stateData))
            {
                EndWeather(ent.Owner, comp, protoId);
                continue;
            }

            weather.StartTime = stateData.StartTime;
            weather.EndTime = stateData.EndTime;
            weather.State = stateData.State;
        }

        foreach (var (protoId, weather) in state.Weather)
        {
            if (comp.Weather.ContainsKey(protoId))
                continue;

            // Without the server's own start time, weather that began hours ago fades in again for a late joiner.
            comp.Weather[protoId] = new WFWeatherData
            {
                StartTime = weather.StartTime,
                EndTime = weather.EndTime,
                State = weather.State,
            };
        }
    }

    // The map can go away without any weather ending, and the sound would keep playing into the lobby.
    private void OnShutdown(Entity<WFWeatherComponent> ent, ref ComponentShutdown args)
    {
        foreach (var weather in ent.Comp.Weather.Values)
            StopStream(weather);
    }

    protected override void Run(EntityUid uid, WFWeatherData weather, WeatherPrototype proto, float frameTime)
    {
        base.Run(uid, weather, proto, frameTime);
        UpdateStream(uid, weather, proto, frameTime);
    }

    protected override bool SetState(EntityUid uid, WFWeatherState state, WFWeatherComponent comp, WFWeatherData weather, WeatherPrototype proto)
    {
        if (!base.SetState(uid, state, comp, weather, proto))
            return false;

        if (!Timing.IsFirstTimePredicted)
            return true;

        RestartStream(weather, proto);
        return true;
    }

    private void StopStream(WFWeatherData weather)
        => weather.Stream = _audio.Stop(weather.Stream);

    private void RestartStream(WFWeatherData weather, WeatherPrototype proto)
    {
        weather.Stream = _audio.Stop(weather.Stream);
        weather.Stream = _audio.PlayGlobal(proto.Sound, Filter.Local(), true)?.Entity;

        if (TryComp(weather.Stream, out AudioComponent? audio))
            audio.Occlusion = ComputeOcclusion(proto);
    }

    private void UpdateStream(EntityUid mapUid, WFWeatherData weather, WeatherPrototype proto, float frameTime)
    {
        if (_player.LocalEntity is not { } ent)
            return;

        var weatherMap = Transform(mapUid).MapUid;
        var xform = Transform(ent);

        if (weatherMap == null || xform.MapUid != weatherMap)
        {
            StopStream(weather);
            return;
        }

        if (!Timing.IsFirstTimePredicted || proto.Sound == null)
            return;

        var streamWasNull = weather.Stream == null;
        weather.Stream ??= _audio.PlayGlobal(proto.Sound, Filter.Local(), true)?.Entity;

        if (!TryComp(weather.Stream, out AudioComponent? comp))
            return;

        // A sound that has just started plays at full volume, so without this the weather is loud for a few seconds.
        if (streamWasNull)
            comp.Occlusion = weather.OcclusionTarget = ComputeOcclusion(proto);

        // Checked on the timer rather than on movement, or a wall breaking open beside someone standing still is never heard.
        weather.OcclusionTimer -= frameTime;

        if (weather.OcclusionTimer <= 0f)
        {
            weather.OcclusionTimer = OcclusionInterval;
            weather.OcclusionTarget = ComputeOcclusion(proto);
        }

        var smoothed = Smooth(comp.Occlusion, weather.OcclusionTarget, frameTime);

        var alpha = GetPercent(weather, mapUid);
        alpha *= SharedAudioSystem.VolumeToGain(proto.Sound.Params.Volume);
        alpha *= GainAttenuationFrom(smoothed);

        _audio.SetGain(weather.Stream, alpha, comp);
        comp.Occlusion = smoothed;
    }

    private float ComputeOcclusion(WeatherPrototype proto)
    {
        if (_player.LocalEntity is not { } ent)
            return 0f;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return 0f;

        var origin = _mapSystem.TileIndicesFor(gridUid, grid, xform.Coordinates);

        var distance = DistanceToWeather(ResolveWeatherGrid((gridUid, grid)), origin, proto);

        return distance switch
        {
            null => OcclusionSilent,
            0 => 0f,
            <= BoundaryDepth => OcclusionBoundary,
            _ => OcclusionInterior,
        };
    }

    // Null when no tile that can have weather is within searching range.
    private int? DistanceToWeather(Entity<MapGridComponent, WFExposureComponent?, RoofComponent?> grid,
        Vector2i origin, WeatherPrototype proto)
    {
        // Walls do not stop sound, so only the straight-line tile distance matters.
        for (var radius = 0; radius <= MaxSearchDepth; radius++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                var y = radius - Math.Abs(x);

                if (Reaches(grid, origin + new Vector2i(x, y), proto)
                    || y != 0 && Reaches(grid, origin + new Vector2i(x, -y), proto))
                    return radius;
            }
        }

        return null;
    }

    private bool Reaches(Entity<MapGridComponent, WFExposureComponent?, RoofComponent?> grid, Vector2i pos,
        WeatherPrototype proto)
        => CanWeatherAffect(grid, _mapSystem.GetTileRef(grid.Owner, grid.Comp1, pos), proto);

    private static float Smooth(float current, float target, float frameTime)
        => current + (target - current) * (1f - MathF.Exp(-frameTime * OcclusionFadeRate));

    // The volume falls off quickly, so standing just inside a window is clearly louder than deep in the room.
    private static float GainAttenuationFrom(float occlusion)
    {
        var clear = Math.Clamp(1f - occlusion / OcclusionSilent, 0f, 1f);
        return clear * clear;
    }
}
