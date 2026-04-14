using Content.Server.NPC.Components;
using Content.Shared.CCVar; // #Misfits Add
using Robust.Shared.Configuration; // #Misfits Add

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat;

public sealed partial class JukeOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!; // #Misfits Add

    [DataField]
    public JukeType JukeType = JukeType.AdjacentTile;

    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.PlanFinished;

    /// <summary>
    ///     Controls how long(in seconds) the NPC will move while juking.
    /// </summary>
    [DataField]
    public float JukeDuration = 0.3f; // #Misfits Change: Reduced from 0.5f to make juking quicker

    /// <summary>
    ///     Controls how often (in seconds) an NPC will try to juke.
    /// </summary>
    [DataField]
    public float JukeCooldown = 6f; // #Misfits Change: Increased from 3f to reduce circling behavior

    /// <summary>
    ///     Distance at which a ranged NPC will retreat from an approaching target.
    ///     Only applies when <see cref="JukeType"/> is <see cref="JukeType.Away"/>.
    /// </summary>
    [DataField]
    public float RetreatDistance = 2.5f;

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var juke = _entManager.EnsureComponent<NPCJukeComponent>(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
        juke.JukeType = JukeType;
        juke.JukeDuration = JukeDuration;

        // #Misfits Add: Allow runtime override of juke cooldown via CVar
        var cooldownOverride = _configManager.GetCVar(CCVars.NPCJukeCooldownOverride);
        juke.JukeCooldown = cooldownOverride > 0f ? cooldownOverride : JukeCooldown;

        juke.RetreatDistance = RetreatDistance;
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        return HTNOperatorStatus.Finished;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        _entManager.RemoveComponent<NPCJukeComponent>(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }
}
