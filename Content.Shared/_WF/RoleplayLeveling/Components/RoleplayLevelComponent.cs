using Robust.Shared.GameStates;

namespace Content.Shared._WF.RoleplayLeveling.Components;

/// <summary>
/// Tracks a player's roleplay level and experience
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RoleplayLevelComponent : Component
{
    /// <summary>
    /// Current roleplay level
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Level = 1;

    /// <summary>
    /// Current experience points
    /// </summary>
    [DataField, AutoNetworkedField]
    public long Experience = 0;

    /// <summary>
    /// Experience required to reach the next level
    /// </summary>
    [DataField, AutoNetworkedField]
    public long ExperienceToNextLevel = 100;

    /// <summary>
    /// Total number of commends received from other players
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TotalCommends = 0;

    /// <summary>
    /// The user's account ID
    /// </summary>
    [DataField]
    public Guid UserId;
}
