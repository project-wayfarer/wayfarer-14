using Robust.Shared.Serialization;

namespace Content.Shared._WF.CommunityGoals.BUI;

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
    /// Prototype ID of the item currently inserted in the contribution slot, or null if empty.
    /// </summary>
    public string? SlottedItemPrototype;

    /// <summary>
    /// How many of the slotted item are there (stack count, or 1 for single items).
    /// </summary>
    public long SlottedItemAmount;

    /// <summary>
    /// Display name of the slotted item (from MetaData).
    /// </summary>
    public string? SlottedItemName;

    public CommunityGoalConsoleState(
        List<CommunityGoalData> activeGoals,
        string? slottedItemPrototype,
        long slottedItemAmount,
        string? slottedItemName)
    {
        ActiveGoals = activeGoals;
        SlottedItemPrototype = slottedItemPrototype;
        SlottedItemAmount = slottedItemAmount;
        SlottedItemName = slottedItemName;
    }
}
