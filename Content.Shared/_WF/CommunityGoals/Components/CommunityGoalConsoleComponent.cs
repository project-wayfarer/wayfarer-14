using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._WF.CommunityGoals.Components;

/// <summary>
/// A station terminal where players can view active community goals and contribute items.
/// Use items on the terminal to stage them, then press Contribute to submit all at once.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CommunityGoalConsoleComponent : Component
{
    /// <summary>
    /// ID of the internal staging container that holds deposited items awaiting commit.
    /// </summary>
    public static readonly string StagingContainerId = "community-goal-staging";

    /// <summary>
    /// Maximum number of item stacks that can sit in the staging area at once.
    /// </summary>
    [DataField]
    public int MaxStagingItems = 20;

    [DataField]
    public SoundSpecifier InsertSound =
        new SoundPathSpecifier("/Audio/Machines/scanning.ogg");

    [DataField]
    public SoundSpecifier CommitSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");
}
