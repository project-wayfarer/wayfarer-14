using System.Linq;
using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._NF.Radar;

/// <summary>
/// The shape of the radar blip.
/// </summary>
[Serializable, NetSerializable]
public enum RadarBlipShape
{
    /// <summary>Circle shape.</summary>
    Circle,
    /// <summary>Square shape.</summary>
    Square,
    /// <summary>Triangle shape.</summary>
    Triangle,
    /// <summary>Star shape.</summary>
    Star,
    /// <summary>Diamond shape.</summary>
    Diamond,
    /// <summary>Hexagon shape.</summary>
    Hexagon,
    /// <summary>Arrow shape.</summary>
    Arrow,
    /// <summary>Heart shape.</summary>
    Heart,
    /// <summary>X.</summary>
    X,
    /// <summary>A cool shape, which is: a circle with a line through it.</summary>
    CircleWithLine,
}

/// <summary>
/// Event sent from the server to the client containing radar blip data.
/// </summary>
[Serializable, NetSerializable]
public sealed class GiveBlipsEvent : EntityEventArgs
{
    /// <summary>
    /// Blips are now (grid entity, position, velocity, scale, color, shape).
    /// If grid entity is null, position and velocity are in world coordinates.
    /// If grid entity is not null, position and velocity are in grid-local coordinates.
    // Wayfarer
    /// Velocity is used by the client to predict/extrapolate blip motion between
    /// the relatively-slow server updates (Wayfarer: predictive radar blips).
    // Wayfarer End
    /// </summary>
    public readonly List<(NetEntity? Grid, Vector2 Position, Vector2 Velocity, float Scale, Color Color, RadarBlipShape Shape)> Blips; // Wayfarer: Add Vector2 Velocity

    /// <summary>
    /// Backwards-compatible constructor for legacy blip format.
    /// </summary>
    /// <param name="blips">List of blips as (position, scale, color).</param>
    public GiveBlipsEvent(List<(Vector2, float, Color)> blips)
    {
        Blips = blips.Select(b => ((NetEntity?)null, b.Item1, Vector2.Zero, b.Item2, b.Item3, RadarBlipShape.Circle)).ToList(); // Wayfarer: Add Vector2.Zero
    }

    /// <summary>
    /// Constructor for the full blip format.
    /// </summary>
    /// <param name="blips">List of blips as (grid, position, velocity, scale, color, shape).</param>
    public GiveBlipsEvent(List<(NetEntity? Grid, Vector2 Position, Vector2 Velocity, float Scale, Color Color, RadarBlipShape Shape)> blips) // Wayfarer: Add Vector2 Velocity
    {
        Blips = blips;
    }
}

/// <summary>
/// A request for radar blips around a given entity.
/// Entity must have the RadarConsoleComponent to receive a response.
/// Requests are rate-limited server-side, unhandled messages will not receive a response.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestBlipsEvent : EntityEventArgs
{
    /// <summary>
    /// The radar entity for which blips are being requested.
    /// </summary>
    public readonly NetEntity Radar;

    /// <summary>
    /// Constructor for RequestBlipsEvent.
    /// </summary>
    /// <param name="radar">The radar entity.</param>
    public RequestBlipsEvent(NetEntity radar)
    {
        Radar = radar;
    }
}

/// <summary>
/// Wayfarer: Notifies clients that the set of radar blips has changed (e.g. a projectile
/// has been spawned) and they should issue an immediate blip request so the new blip
/// shows up without waiting for the regular polling interval.
/// </summary>
[Serializable, NetSerializable]
public sealed class RadarBlipsDirtyEvent : EntityEventArgs
{
}
