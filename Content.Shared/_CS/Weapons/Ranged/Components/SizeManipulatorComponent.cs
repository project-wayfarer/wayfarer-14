using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Component for guns that can toggle between growing and shrinking targets.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SizeManipulatorComponent : Component
{
    /// <summary>
    /// Current mode of the size manipulator (grow or shrink)
    /// </summary>
    [DataField, AutoNetworkedField]
    public SizeManipulatorMode Mode = SizeManipulatorMode.Grow;

    /// <summary>
    /// The grow hitscan prototype ID
    /// </summary>
    [DataField(required: true)]
    public string GrowPrototype = string.Empty;

    /// <summary>
    /// The shrink hitscan prototype ID
    /// </summary>
    [DataField(required: true)]
    public string ShrinkPrototype = string.Empty;
}

/// <summary>
/// Component for the projectiles fired by size manipulator guns.
/// Stores which mode (grow/shrink) this projectile should apply.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BulletSizeManipulatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public SizeManipulatorMode Mode = SizeManipulatorMode.Grow;
}

[Serializable, NetSerializable]
public enum SizeManipulatorMode : byte
{
    Grow,
    Shrink
}
