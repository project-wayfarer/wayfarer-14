using Robust.Shared.GameStates;

namespace Content.Shared._WF.Shuttles.Components;

/// <summary>
/// Stores the time a docking port became docked. SR QOL for people being naughty with the docks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DockTimestampComponent : Component
{
    /// <summary>
    /// The game time when docking started, or null if not docked.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan? DockStartTime;
}
