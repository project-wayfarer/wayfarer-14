using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Clothing.Components;

/// <summary>
/// When applied to a collar (or other clothing item), prevents clothing from being removed
/// while this item is worn. This includes both self-removal and removal by others.
/// Can be configured to lock specific slots or all slots.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
[Access(typeof(ClothingLockSystem))]
public sealed partial class ClothingLockComponent : Component
{
    /// <summary>
    /// List of inventory slot names to lock. If empty or null, locks all slots.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public List<string>? LockedSlots;

    // WF Psychic clothing lock
    /// <summary>
    /// Whether to block unequipping by others or only by wearer
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool? BlockOthers;
}
