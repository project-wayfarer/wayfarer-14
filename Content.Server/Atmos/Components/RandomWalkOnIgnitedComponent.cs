namespace Content.Server.Atmos.Components;

/// <summary>
/// Adds/removes RandomWalkComponent when the entity is ignited/extinguished.
/// Used for entities like paper lanterns that should only drift when lit.
/// </summary>
[RegisterComponent]
public sealed partial class RandomWalkOnIgnitedComponent : Component
{
}
