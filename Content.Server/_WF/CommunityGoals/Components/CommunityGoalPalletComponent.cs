namespace Content.Server._WF.CommunityGoals.Components;

/// <summary>
/// Marks a floor tile as a community goal donation pallet.
/// Any items sitting on this tile when the linked community goal console commits
/// will be contributed toward matching active goal requirements.
/// Large items like gas canisters that cannot be inserted directly into the console
/// can be placed on a pallet and donated this way.
/// </summary>
[RegisterComponent]
public sealed partial class CommunityGoalPalletComponent : Component { }
