using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Emberfall.Weapons.Ranged;

/// <summary>
/// Added to projectiles to give them tracer effects
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TracerComponent : Component
{
    /// <summary>
    /// How long the tracer effect should remain visible for after firing
    /// </summary>
    [DataField]
    public float Lifetime = .5f; // Wayfarer - Tracers

    /// <summary>
    /// The maximum length of the tracer trail
    /// </summary>
    [DataField]
    public float Length = 50f; // Wayfarer - Tracers

    /// <summary>
    /// Color of the tracer line effect
    /// </summary>
    [DataField]
    public Color Color = Color.Red;
}
