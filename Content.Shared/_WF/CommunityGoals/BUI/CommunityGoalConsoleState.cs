using Robust.Shared.Serialization;

namespace Content.Shared._WF.CommunityGoals.BUI;

/// <summary>
/// One entry in the staging area — items are grouped by prototype so stacks of the same
/// type are shown as a single row.
/// </summary>
[Serializable, NetSerializable]
public sealed class StagedItemData
{
    public string PrototypeId;
    public string DisplayName;
    public long Amount;

    public StagedItemData(string prototypeId, string displayName, long amount)
    {
        PrototypeId = prototypeId;
        DisplayName = displayName;
        Amount = amount;
    }
}

/// <summary>
/// State pushed from the server to the client whenever the console UI is open.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunityGoalConsoleState : BoundUserInterfaceState
{
    /// <summary>
    /// All goals that are active in this round.
    /// </summary>
    public List<CommunityGoalData> ActiveGoals;

    /// <summary>
    /// Items currently staged for contribution (grouped by prototype ID).
    /// </summary>
    public List<StagedItemData> StagedItems;

    public CommunityGoalConsoleState(List<CommunityGoalData> activeGoals, List<StagedItemData> stagedItems)
    {
        ActiveGoals = activeGoals;
        StagedItems = stagedItems;
    }
}
