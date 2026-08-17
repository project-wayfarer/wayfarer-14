namespace Content.Server._WF.CommunityGoals.Components;

/// <summary>
/// Tracks which kill-order requirement IDs this entity's death has already been credited
/// toward. Prevents reviving a mob and killing it again from counting as a second kill
/// against the same requirement.
/// </summary>
[RegisterComponent]
public sealed partial class CommunityGoalKillCreditComponent : Component
{
    [DataField]
    public HashSet<int> CreditedRequirements = new();
}
