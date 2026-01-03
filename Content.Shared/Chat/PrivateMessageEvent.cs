using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Chat;

/// <summary>
/// Event sent from server to client when a private message is received
/// </summary>
[Serializable, NetSerializable]
public sealed class PrivateMessageEvent : EntityEventArgs
{
    /// <summary>
    /// The username of the sender
    /// </summary>
    public string SenderUsername { get; }

    /// <summary>
    /// The character name of the sender, if applicable
    /// </summary>
    public string? SenderCharacterName { get; }

    /// <summary>
    /// The NetUserId of the sender for reply tracking
    /// </summary>
    public NetUserId SenderUserId { get; }

    /// <summary>
    /// The message content
    /// </summary>
    public string Message { get; }

    public PrivateMessageEvent(string senderUsername, string? senderCharacterName, NetUserId senderUserId, string message)
    {
        SenderUsername = senderUsername;
        SenderCharacterName = senderCharacterName;
        SenderUserId = senderUserId;
        Message = message;
    }
}
