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
    private static readonly List<(Vector2, float, Color, RadarBlipShape)> EmptyBlipList = new();
    private static readonly List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> EmptyRawBlipList = new();
    private TimeSpan _lastRequestTime = TimeSpan.Zero;
    // Minimum time between requests.  Slightly larger than the server-side value.
    private static readonly TimeSpan RequestThrottle = TimeSpan.FromMilliseconds(300); // Wayfarer: 1250<300

    // Maximum distance for blips to be considered visible
    private const float MaxBlipRenderDistance = 256f;
    private const float MaxBlipRenderDistanceSquared = MaxBlipRenderDistance * MaxBlipRenderDistance;
    // Minimum radar position change to invalidate cache (in units)
    private const float RadarPositionChangeThreshold = 5f;
    private const float RadarPositionChangeThresholdSquared = RadarPositionChangeThreshold * RadarPositionChangeThreshold;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private TimeSpan _lastUpdatedTime;
    private List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> _blips = new();
    private Vector2 _radarWorldPosition;

    // Cached filtered results
    private List<(Vector2, float, Color, RadarBlipShape)> _cachedBlips = new();
    private List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> _cachedRawBlips = new();
    private bool _cacheValid = false;
    private Vector2 _cachedRadarPosition;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GiveBlipsEvent>(HandleReceiveBlips);
    }

    /// <summary>
    /// Handles receiving blip data from the server.
    /// </summary>
    private void HandleReceiveBlips(GiveBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (ev?.Blips == null)
        {
            _blips = EmptyRawBlipList;
            _cacheValid = false;
            return;
        }
        _blips = ev.Blips;
        _lastUpdatedTime = _timing.CurTime;
        _cacheValid = false;
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

        // Cache the radar position for distance culling
        var newRadarPosition = _xform.GetWorldPosition(console);

        // Invalidate cache if radar moved significantly
        if (Vector2.DistanceSquared(newRadarPosition, _cachedRadarPosition) > RadarPositionChangeThresholdSquared)
        {
            _cacheValid = false;
            _cachedRadarPosition = newRadarPosition;
        }

        _radarWorldPosition = newRadarPosition;

        var netConsole = GetNetEntity(console);
        var ev = new RequestBlipsEvent(netConsole);
        RaiseNetworkEvent(ev);
    }

    /// <summary>
    /// Gets the current blips as world positions with their scale, color and shape.
    /// This is needed for the legacy radar display that expects world coordinates.
    /// </summary>
    public List<(Vector2, float, Color, RadarBlipShape)> GetCurrentBlips()
    {
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return EmptyBlipList;

        if (_cacheValid)
            return _cachedBlips;

        UpdateCache();
        return _cachedBlips;
    }

    /// <summary>
    /// Gets the raw blips data which includes grid information for more accurate rendering.
    /// </summary>
    public List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> GetRawBlips()
    {
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return EmptyRawBlipList;

        if (_blips.Count == 0)
            return _blips;

        if (_cacheValid)
            return _cachedRawBlips;

        UpdateCache();
        return _cachedRawBlips;
    }

    /// <summary>
    /// Updates both caches by filtering blips based on distance.
    /// </summary>
    private void UpdateCache()
    {
        _cachedBlips.Clear();
        _cachedRawBlips.Clear();

        foreach (var blip in _blips)
        {
            Vector2 worldPosition;

            if (blip.Grid == null)
            {
                worldPosition = blip.Position;

                // Distance culling for world position blips
                if (Vector2.DistanceSquared(worldPosition, _radarWorldPosition) > MaxBlipRenderDistanceSquared)
                    continue;

                _cachedBlips.Add((worldPosition, blip.Scale, blip.Color, blip.Shape));
                _cachedRawBlips.Add(blip);
                continue;
            }

            if (TryGetEntity(blip.Grid, out var gridEntity))
            {
                var worldPos = _xform.GetWorldPosition(gridEntity.Value);
                var gridRot = _xform.GetWorldRotation(gridEntity.Value);
                var rotatedLocalPos = gridRot.RotateVec(blip.Position);
                worldPosition = worldPos + rotatedLocalPos;

                // Distance culling for grid position blips
                if (Vector2.DistanceSquared(worldPosition, _radarWorldPosition) > MaxBlipRenderDistanceSquared)
                    continue;

                _cachedBlips.Add((worldPosition, blip.Scale, blip.Color, blip.Shape));
                _cachedRawBlips.Add(blip);
            }
        }

        _cacheValid = true;
    }
}
