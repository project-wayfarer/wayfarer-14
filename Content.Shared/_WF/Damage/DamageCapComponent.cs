using Robust.Shared.GameStates;

namespace Content.Shared._WF.Damage;

[RegisterComponent, NetworkedComponent]
public sealed partial class DamageCapComponent : Component
{
    /// <summary>
    /// Maximum allowed damage for all damage types. (Yes, this is generalized. Fight me.) A cap of 3000 means you can have, for example, 3000 burn, 3000 brute, 3000 slash, so 3000 in any type.
    /// </summary>
    [DataField]
    public int DamageCap = 3500;
}
