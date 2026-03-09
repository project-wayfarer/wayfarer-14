using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Silicons.Bots;
using Robust.Shared.Audio.Systems;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Operator for replacing a broken light bulb in a fixture.
/// </summary>
public sealed partial class LightbotReplaceLightOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    private LightReplacerSystem _lightReplacer = default!;
    private SharedAudioSystem _audio = default!;

    /// <summary>
    /// Target light fixture entity to replace.
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lightReplacer = sysManager.GetEntitySystem<LightReplacerSystem>();
        _audio = sysManager.GetEntitySystem<SharedAudioSystem>();
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        blackboard.Remove<EntityUid>(TargetKey);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entMan) || _entMan.Deleted(target))
            return HTNOperatorStatus.Failed;

        if (!_entMan.TryGetComponent<LightbotComponent>(owner, out var botComp))
            return HTNOperatorStatus.Failed;

        if (!_entMan.TryGetComponent<PoweredLightComponent>(target, out var fixture))
            return HTNOperatorStatus.Failed;

        if (!_entMan.TryGetComponent<LightReplacerComponent>(owner, out var replacer))
            return HTNOperatorStatus.Failed;

        // Try to replace the bulb
        var success = _lightReplacer.TryReplaceBulb(owner, target, null, replacer, fixture);

        if (!success)
            return HTNOperatorStatus.Failed;

        return HTNOperatorStatus.Finished;
    }
}
