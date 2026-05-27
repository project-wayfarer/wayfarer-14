using Robust.Shared.GameStates;

namespace Content.Shared._WF.Traits;

/// <summary>
/// Marks an entity as having a clay body. Such entities can be "plucked" by others
/// to remove a small amount of clay (shrinking them), which drops a ClayChunk item.
/// The entity slowly regenerates its size over time.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class WFClayBodyComponent : Component
{
    /// <summary>
    /// How long between each regen tick (grows the entity back by one size step).
    /// </summary>
    [DataField]
    public TimeSpan RegenInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The game time at which the next regen tick should occur.
    /// Null means the timer is not currently running (entity is at full size).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? NextRegenTime = null;

    /// <summary>
    /// The scale the entity should regen back to. Captured on first pluck.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OriginalScale = 1.0f;

    /// <summary>
    /// Whether the original scale has been captured yet.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OriginalScaleCaptured = false;
}
