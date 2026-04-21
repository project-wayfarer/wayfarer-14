using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._WF.CommunityGoals.Components;

/// <summary>
/// An in-station terminal where players can view active community goals and contribute items.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CommunityGoalConsoleComponent : Component
{
    public static readonly string SlotId = "community-goal-console-slot";

    [DataField]
    public ItemSlot ItemSlot = new();

    [DataField]
    public SoundSpecifier ContributeSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");
}
