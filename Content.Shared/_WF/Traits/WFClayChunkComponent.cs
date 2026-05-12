namespace Content.Shared._WF.Traits;

/// <summary>
/// Marks this item as a clay chunk that was plucked from a Clay Body player.
/// When used on a player with <see cref="WFClayBodyComponent"/>, it is consumed
/// to restore one size step to the target.
/// </summary>
[RegisterComponent]
public sealed partial class WFClayChunkComponent : Component;
