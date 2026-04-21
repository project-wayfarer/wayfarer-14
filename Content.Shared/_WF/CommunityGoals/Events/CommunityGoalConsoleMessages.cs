using Robust.Shared.Serialization;

namespace Content.Shared._WF.CommunityGoals.Events;

/// <summary>
/// Sent by the client when the player presses "Contribute" to submit the item in the slot.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunityGoalContributeMessage : BoundUserInterfaceMessage
{
}
