using Robust.Shared.GameStates;

namespace Content.Shared._WF.Weather;

// Per-grid tile sets, built on the server and sent to clients.
// Exposed: Tiles currently open to space.
// Rooved: Tiles ever covered by a roof, kept across wall changes.
[RegisterComponent, UnsavedComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WFExposureComponent : Component
{
    [AutoNetworkedField]
    public HashSet<Vector2i> Exposed = new();

    [AutoNetworkedField]
    public HashSet<Vector2i> Rooved = new();
}
