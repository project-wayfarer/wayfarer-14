using Robust.Shared.Serialization;

namespace Content.Shared._WF.CommunityGoals.Events;

/// <summary>
/// Sent by the client when the player presses "Contribute All" to submit everything in the staging area.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunityGoalCommitMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Sent by the client when the player presses "Return Items" to eject all staged items back to the floor.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunityGoalClearStagingMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Sent by the client when the player presses the per-requirement "Contribute" button.
/// Only staged items that match this specific requirement will be consumed and recorded.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunityGoalContributeToRequirementMessage : BoundUserInterfaceMessage
{
    public int RequirementId;

    public CommunityGoalContributeToRequirementMessage(int requirementId)
    {
        RequirementId = requirementId;
    }
}
