using Robust.Shared.GameStates;

namespace Content.Shared._WF.RCD.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipyardRCDComponent : Component
{
    /// <summary>
    /// Indicates this RCD is restricted to authorized ship grids.
	/// ~~~ Wayfarer ~~~
    /// Moved flag from RCDComponent to its own file within the _WF directory
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsShipyardRCD = true;
}