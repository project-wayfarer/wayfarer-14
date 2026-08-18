using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Coyote.ShuttleCrewStatus;

/// <summary>
/// Component that tracks shuttle crew status and manages IFF color based on active players aboard.
/// </summary>
[RegisterComponent]
public sealed partial class ShuttleCrewStatusComponent : Component
{
    /// <summary>
    /// The original IFF color before any crew status changes.
    /// Used to restore the color when active players are detected.
    /// </summary>
    [DataField]
    public Color? OriginalColor;

    /// <summary>
    /// Whether the shuttle currently has active players aboard.
    /// </summary>
    [DataField]
    public bool HasActiveCrew;

    /// <summary>
    /// The next time to check crew status.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextCheck = TimeSpan.Zero;

    /// <summary>
    /// The time the grid was first observed to have no active (non-ghost) players aboard,
    /// with no active players aboard since. Null while the grid is occupied.
    /// Used to require a sustained empty period before marking the shuttle inactive.
    /// </summary>
    [DataField]
    public TimeSpan? EmptySince;

    /// <summary>
    /// The time the grid was first observed to have an active (non-ghost) player aboard,
    /// with an active player aboard continuously since. Null while the grid is empty.
    /// Used to require a sustained occupied period before marking the shuttle active.
    /// </summary>
    [DataField]
    public TimeSpan? OccupiedSince;
}
