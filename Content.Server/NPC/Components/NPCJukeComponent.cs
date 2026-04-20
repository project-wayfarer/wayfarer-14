using Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.NPC.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class NPCJukeComponent : Component
{
    [DataField]
    public JukeType JukeType = JukeType.Away;

    [DataField]
    public float JukeDuration = 0.5f;

    [DataField]
    public float JukeCooldown = 3f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextJuke;

    [DataField]
    public Vector2i? TargetTile;

    /// <summary>
    /// Distance at which a ranged NPC will try to back away from an approaching target.
    /// Only used when <see cref="JukeType"/> is <see cref="JukeType.Away"/> and the NPC has
    /// an active <see cref="NPCRangedCombatComponent"/>.
    /// </summary>
    [DataField]
    public float RetreatDistance = 4f;
}

public enum JukeType : byte
{
    /// <summary>
    /// Will move directly away from target if applicable.
    /// </summary>
    Away,

    /// <summary>
    /// Move to the adjacent tile for the specified duration.
    /// </summary>
    AdjacentTile
}
