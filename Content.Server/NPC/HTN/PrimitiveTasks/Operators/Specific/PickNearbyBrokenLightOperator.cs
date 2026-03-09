using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Light.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.Silicons.Bots;
using Content.Shared.Interaction;
using Content.Shared.NPC.Components;
using Content.Shared.Silicons.Bots;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Operator for finding nearby broken lights that need replacement.
/// </summary>
public sealed partial class PickNearbyBrokenLightOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private LightbotSystem _lightbot = default!;
    private PathfindingSystem _pathfinding = default!;

    [DataField("rangeKey")] 
    public string RangeKey = "LightbotRange";

    /// <summary>
    /// Target light fixture entity to replace.
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    /// <summary>
    /// Target coordinates to move to.
    /// </summary>
    [DataField("targetMoveKey", required: true)]
    public string TargetMoveKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lightbot = sysManager.GetEntitySystem<LightbotSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<float>(RangeKey, out var range, _entManager))
            return (false, null);

        if (!_entManager.TryGetComponent<LightbotComponent>(owner, out var lightbot))
            return (false, null);

        // Find all broken lights in range
        var brokenLights = _lightbot.GetBrokenLightsInRange(owner, range).ToList();

        if (brokenLights.Count == 0)
            return (false, null);

        // Pick the closest broken light
        EntityUid? bestTarget = null;
        var bestDistance = float.MaxValue;
        var ownerXform = _entManager.GetComponent<TransformComponent>(owner);

        foreach (var light in brokenLights)
        {
            var lightXform = _entManager.GetComponent<TransformComponent>(light);
            
            // Skip if on different map
            if (lightXform.MapID != ownerXform.MapID)
                continue;

            var distance = (lightXform.WorldPosition - ownerXform.WorldPosition).Length();
            
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = light;
            }
        }

        if (bestTarget == null)
            return (false, null);

        var targetXform = _entManager.GetComponent<TransformComponent>(bestTarget.Value);
        
        // Check if we can path to the target
        var pathRange = SharedInteractionSystem.InteractionRange - 0.5f;
        var path = await _pathfinding.GetPath(owner, bestTarget.Value, pathRange, cancelToken);

        if (path.Result != PathResult.Path)
            return (false, null);

        return (true, new Dictionary<string, object>
        {
            { TargetKey, bestTarget.Value },
            { TargetMoveKey, targetXform.Coordinates },
            { NPCBlackboard.PathfindKey, path }
        });
    }
}
