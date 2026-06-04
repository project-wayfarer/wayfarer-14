using System.Numerics;
using Content.Shared._NF.Radar;
using Robust.Shared.Timing;

namespace Content.Client._NF.Radar;

/// <summary>
/// A system for requesting, receiving, and caching radar blips.
/// Sends off ad hoc requests for blips, caches them for a period of time, and draws them when requested.
/// </summary>
/// <remarks>
/// Ported from Monolith's RadarBlipsSystem.
/// </remarks>
public sealed partial class RadarBlipSystem : EntitySystem
{
    private const double BlipStaleSeconds = 3.0;
    // Wayfarer: cap how far we extrapolate a blip past its received position so a
    // stopped or destroyed projectile can't run away from its real location.
    private const double MaxPredictionSeconds = 1.0;
    private static readonly List<(Vector2, float, Color, RadarBlipShape)> EmptyBlipList = new();
    private static readonly List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> EmptyRawBlipList = new();
    // Wayfarer: empty list matching the wire-format tuple (which now includes per-blip velocity).
    private static readonly List<(NetEntity? Grid, Vector2 Position, Vector2 Velocity, float Scale, Color Color, RadarBlipShape Shape)> EmptyReceivedBlipList = new();
    private TimeSpan _lastRequestTime = TimeSpan.Zero;
    // Minimum time between requests.  Slightly larger than the server-side value.
    private static readonly TimeSpan RequestThrottle = TimeSpan.FromMilliseconds(300); // Wayfarer: 1250<300

    // Maximum distance for blips to be considered visible
    private const float MaxBlipRenderDistance = 256f;
    private const float MaxBlipRenderDistanceSquared = MaxBlipRenderDistance * MaxBlipRenderDistance;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private TimeSpan _lastUpdatedTime;
    private List<(NetEntity? Grid, Vector2 Position, Vector2 Velocity, float Scale, Color Color, RadarBlipShape Shape)> _blips = new();
    private Vector2 _radarWorldPosition;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GiveBlipsEvent>(HandleReceiveBlips);
        // Wayfarer: server tells us when a new blip entity appears so we can request a fresh
        // list immediately instead of waiting on the polling throttle.
        SubscribeNetworkEvent<RadarBlipsDirtyEvent>(HandleBlipsDirty);
    }

    /// <summary>
    /// Wayfarer: drop the request throttle so the next radar frame issues an immediate
    /// blip request. Useful for instantly showing newly-spawned projectiles.
    /// </summary>
    private void HandleBlipsDirty(RadarBlipsDirtyEvent ev, EntitySessionEventArgs args)
    {
        _lastRequestTime = TimeSpan.Zero;
    }

    /// <summary>
    /// Handles receiving blip data from the server.
    /// </summary>
    private void HandleReceiveBlips(GiveBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (ev?.Blips == null)
        {
            _blips = EmptyReceivedBlipList;
            return;
        }
        _blips = ev.Blips;
        _lastUpdatedTime = _timing.CurTime;
    }

    /// <summary>
    /// Requests blip data from the server for the given radar console, throttled to avoid spamming.
    /// </summary>
    public void RequestBlips(EntityUid console)
    {
        if (!Exists(console))
            return;

        if (_timing.CurTime - _lastRequestTime < RequestThrottle)
            return;

        _lastRequestTime = _timing.CurTime;

        _radarWorldPosition = _xform.GetWorldPosition(console);

        var netConsole = GetNetEntity(console);
        var ev = new RequestBlipsEvent(netConsole);
        RaiseNetworkEvent(ev);
    }

    /// <summary>
    /// Gets the current blips as world positions with their scale, color and shape.
    /// Wayfarer: positions are extrapolated from the last received server snapshot using the
    /// per-blip linear velocity, so fast-moving entities like shipgun projectiles get a smooth
    /// per-frame radar refresh rate instead of stepping every ~250 ms.
    /// </summary>
    public List<(Vector2, float, Color, RadarBlipShape)> GetCurrentBlips()
    {
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return EmptyBlipList;

        var result = new List<(Vector2, float, Color, RadarBlipShape)>(_blips.Count);
        BuildPredicted(rawOutput: null, worldOutput: result);
        return result;
    }

    /// <summary>
    /// Gets the raw blips data which includes grid information for more accurate rendering.
    /// Wayfarer: positions are extrapolated using each blip's reported linear velocity so the
    /// rendered position keeps up with the entity between server snapshots.
    /// </summary>
    public List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> GetRawBlips()
    {
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return EmptyRawBlipList;

        if (_blips.Count == 0)
            return EmptyRawBlipList;

        var result = new List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)>(_blips.Count);
        BuildPredicted(rawOutput: result, worldOutput: null);
        return result;
    }

    /// <summary>
    /// Builds the predicted blip lists by extrapolating each blip's last known position with
    /// its linear velocity. Either output list may be null when only one is needed.
    /// </summary>
    private void BuildPredicted(
        List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)>? rawOutput,
        List<(Vector2, float, Color, RadarBlipShape)>? worldOutput)
    {
        // How long since the latest snapshot - capped to keep stale predictions from running away.
        var elapsed = (_timing.CurTime - _lastUpdatedTime).TotalSeconds;
        if (elapsed < 0)
            elapsed = 0;
        else if (elapsed > MaxPredictionSeconds)
            elapsed = MaxPredictionSeconds;
        var dt = (float)elapsed;

        foreach (var blip in _blips)
        {
            // Extrapolate in the same coordinate frame the position was sent in
            // (world if Grid is null, grid-local otherwise).
            var predictedLocal = blip.Position + blip.Velocity * dt;

            Vector2 worldPosition;
            if (blip.Grid == null)
            {
                worldPosition = predictedLocal;

                if (Vector2.DistanceSquared(worldPosition, _radarWorldPosition) > MaxBlipRenderDistanceSquared)
                    continue;
            }
            else if (TryGetEntity(blip.Grid, out var gridEntity))
            {
                var worldPos = _xform.GetWorldPosition(gridEntity.Value);
                var gridRot = _xform.GetWorldRotation(gridEntity.Value);
                worldPosition = worldPos + gridRot.RotateVec(predictedLocal);

                if (Vector2.DistanceSquared(worldPosition, _radarWorldPosition) > MaxBlipRenderDistanceSquared)
                    continue;
            }
            else
            {
                continue;
            }

            worldOutput?.Add((worldPosition, blip.Scale, blip.Color, blip.Shape));
            // For raw output, ship the predicted local position so consumers that re-transform
            // through the current grid matrix still get a smoothly-interpolated blip.
            rawOutput?.Add((blip.Grid, predictedLocal, blip.Scale, blip.Color, blip.Shape));
        }
    }
}
