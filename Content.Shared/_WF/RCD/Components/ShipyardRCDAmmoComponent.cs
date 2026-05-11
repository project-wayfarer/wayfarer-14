using Robust.Shared.GameStates;

namespace Content.Shared._WF.RCD.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipyardRCDAmmoComponent : Component
{
    /// <summary>
    /// ~~~ Frontier ~~~
    /// A flag that limits RCD to the authorized ships.
	/// ~~~ Wayfarer ~~~
    /// Moved flag from RCDAmmoComponent to its own file within the _WF directory
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsShipyardRCDAmmo = true;
}