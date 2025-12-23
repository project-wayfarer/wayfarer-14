namespace Content.Server.Atmos.Components;

/// <summary>
/// Marker component that prevents FlammableComponent from being extinguished in airless/low oxygen environments.
/// Used for things like paper lanterns that should continue burning in space.
/// </summary>
[RegisterComponent]
public sealed partial class AirlessFlammableComponent : Component
{
}
