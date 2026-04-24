using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.CommunityGoals;

// ──────────────────────────────────────────────────────
//  Serializable data records transferred over the network
// ──────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class CommunityGoalRequirementData
{
    public int Id;
    public string EntityPrototypeId = string.Empty;
    public string? DisplayName;
    public long RequiredAmount;
    public long CurrentAmount;
}

[Serializable, NetSerializable]
public sealed class CommunityGoalData
{
    public int Id;
    public string Title = string.Empty;
    public string Description = string.Empty;
    public int? StartRound;
    public int? EndRound;
    public bool IsActive;
    public List<CommunityGoalRequirementData> Requirements = new();
}

// ──────────────────────────────────────────────────────
//  EUI State
// ──────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class CommunityGoalsEuiState : EuiStateBase
{
    public List<CommunityGoalData> Goals;
    public int CurrentRound;

    public CommunityGoalsEuiState(List<CommunityGoalData> goals, int currentRound)
    {
        Goals = goals;
        CurrentRound = currentRound;
    }
}

// ──────────────────────────────────────────────────────
//  EUI Messages (client → server)
// ──────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class CreateCommunityGoalMessage : EuiMessageBase
{
    public string Title;
    public string Description;
    public int? StartRound;
    public int? EndRound;

    public CreateCommunityGoalMessage(string title, string description, int? startRound, int? endRound)
    {
        Title = title;
        Description = description;
        StartRound = startRound;
        EndRound = endRound;
    }
}

[Serializable, NetSerializable]
public sealed class UpdateCommunityGoalMessage : EuiMessageBase
{
    public int GoalId;
    public string Title;
    public string Description;
    public int? StartRound;
    public int? EndRound;
    public bool IsActive;

    public UpdateCommunityGoalMessage(int goalId, string title, string description, int? startRound, int? endRound, bool isActive)
    {
        GoalId = goalId;
        Title = title;
        Description = description;
        StartRound = startRound;
        EndRound = endRound;
        IsActive = isActive;
    }
}

[Serializable, NetSerializable]
public sealed class DeleteCommunityGoalMessage : EuiMessageBase
{
    public int GoalId;

    public DeleteCommunityGoalMessage(int goalId)
    {
        GoalId = goalId;
    }
}

[Serializable, NetSerializable]
public sealed class AddCommunityGoalRequirementMessage : EuiMessageBase
{
    public int GoalId;
    public string EntityPrototypeId;
    public string? DisplayName;
    public long RequiredAmount;

    public AddCommunityGoalRequirementMessage(int goalId, string entityPrototypeId, string? displayName, long requiredAmount)
    {
        GoalId = goalId;
        EntityPrototypeId = entityPrototypeId;
        DisplayName = displayName;
        RequiredAmount = requiredAmount;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveCommunityGoalRequirementMessage : EuiMessageBase
{
    public int RequirementId;

    public RemoveCommunityGoalRequirementMessage(int requirementId)
    {
        RequirementId = requirementId;
    }
}

[Serializable, NetSerializable]
public sealed class UpdateCommunityGoalRequirementMessage : EuiMessageBase
{
    public int RequirementId;
    public long RequiredAmount;

    public UpdateCommunityGoalRequirementMessage(int requirementId, long requiredAmount)
    {
        RequirementId = requirementId;
        RequiredAmount = requiredAmount;
    }
}
