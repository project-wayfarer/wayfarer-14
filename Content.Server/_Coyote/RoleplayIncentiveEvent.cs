using Content.Shared.Chat;
using Content.Shared.Radio;

namespace Content.Server._Coyote;

/// <summary>
/// This is the event raised when a roleplay incentive action is taken.
/// </summary>
public sealed class RoleplayIncentiveEvent(
    EntityUid source,
    ChatChannel channel,
    string message,
    int peoplePresent = 0
    )
    : EntityEventArgs
{
    public readonly EntityUid Source = source;
    public readonly ChatChannel Channel = channel;
    public readonly string Message = message;
    public readonly int PeoplePresent = peoplePresent;
}
