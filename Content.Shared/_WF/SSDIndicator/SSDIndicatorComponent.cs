using Content.Shared.CCVar;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.SSDIndicator;

/// <summary>
/// Shows status icon when an entity is SSD, based on if a player is attached or not.
/// </summary>
public sealed partial class SSDIndicatorComponent
{
    /// <summary>
    /// They went SSD at this time.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public TimeSpan WentSSDAt = TimeSpan.Zero;

    /// <summary>
    /// The job that was opened when they went SSD.
    /// Prevents reopening the job if they go SSD again within a certain time frame.
    /// </summary>
    public bool JobOpened = false;
}
