using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Chat.Systems;

/// <summary>
/// Handles private messaging between players
/// </summary>
public sealed class PrivateMessageSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    /// <summary>
    /// Tracks the last person each player received a private message from for /reply
    /// Key: recipient's NetUserId, Value: sender's NetUserId
    /// </summary>
    private readonly Dictionary<NetUserId, NetUserId> _lastPrivateMessageSender = new();

    public override void Initialize()
    {
        base.Initialize();
    }

    /// <summary>
    /// Sends a private message from one player to another
    /// </summary>
    /// <param name="sender">The player sending the message</param>
    /// <param name="targetIdentifier">The username or character name of the recipient</param>
    /// <param name="message">The message content</param>
    /// <returns>True if the message was sent successfully</returns>
    public bool SendPrivateMessage(ICommonSession sender, string targetIdentifier, string message)
    {
        // Validate message length
        if (_chatManager.MessageCharacterLimit(sender, message))
        {
            _chatManager.DispatchServerMessage(sender, $"Your message is too long!");
            return false;
        }

        // Find the target player
        var target = FindPlayer(targetIdentifier);
        if (target == null)
        {
            _chatManager.DispatchServerMessage(sender, $"Could not find player '{targetIdentifier}'");
            return false;
        }

        // Don't allow sending messages to yourself
        if (target.UserId == sender.UserId)
        {
            _chatManager.DispatchServerMessage(sender, "You cannot send a private message to yourself!");
            return false;
        }

        // Get sender's character name if they have an entity
        string? senderCharacterName = null;
        if (sender.AttachedEntity is { } senderEntity)
        {
            senderCharacterName = Identity.Name(senderEntity, EntityManager);
        }

        // Send the message
        SendPrivateMessageInternal(sender, target, message, senderCharacterName);
        
        return true;
    }

    /// <summary>
    /// Sends a reply to the last person who sent a private message
    /// </summary>
    /// <param name="sender">The player sending the reply</param>
    /// <param name="message">The message content</param>
    /// <returns>True if the reply was sent successfully</returns>
    public bool SendReply(ICommonSession sender, string message)
    {
        // Validate message length
        if (_chatManager.MessageCharacterLimit(sender, message))
        {
            _chatManager.DispatchServerMessage(sender, $"Your message is too long!");
            return false;
        }

        // Check if there's someone to reply to
        if (!_lastPrivateMessageSender.TryGetValue(sender.UserId, out var targetUserId))
        {
            _chatManager.DispatchServerMessage(sender, "You have no one to reply to!");
            return false;
        }

        // Check if the target is still online
        if (!_playerManager.TryGetSessionById(targetUserId, out var target))
        {
            _chatManager.DispatchServerMessage(sender, "The player you're trying to reply to is no longer online!");
            _lastPrivateMessageSender.Remove(sender.UserId);
            return false;
        }

        // Get sender's character name if they have an entity
        string? senderCharacterName = null;
        if (sender.AttachedEntity is { } senderEntity)
        {
            senderCharacterName = Identity.Name(senderEntity, EntityManager);
        }

        // Send the message
        SendPrivateMessageInternal(sender, target, message, senderCharacterName);
        
        return true;
    }

    /// <summary>
    /// Internal method to actually send the private message
    /// </summary>
    private void SendPrivateMessageInternal(ICommonSession sender, ICommonSession target, string message, string? senderCharacterName)
    {
        // Update reply tracking
        _lastPrivateMessageSender[target.UserId] = sender.UserId;

        // Create the event
        var pmEvent = new PrivateMessageEvent(sender.Name, senderCharacterName, sender.UserId, message);

        // Send to recipient
        RaiseNetworkEvent(pmEvent, target.Channel);

        // Send confirmation to sender
        var senderMessage = $"[PM to {target.Name}]: {message}";
        _chatManager.DispatchServerMessage(sender, senderMessage);

        // Log the private message
        _adminLogger.Add(LogType.Chat, LogImpact.Low, 
            $"PM from {sender.Name} to {target.Name}: {message}");
    }

    /// <summary>
    /// Finds a player by username or character name (up to first space)
    /// </summary>
    /// <param name="identifier">The username or character name to search for</param>
    /// <returns>The matching session, or null if not found</returns>
    private ICommonSession? FindPlayer(string identifier)
    {
        // First, try exact username match
        if (_playerManager.TryGetSessionByUsername(identifier, out var session))
        {
            return session;
        }

        // Try to find by character name (full or partial up to first space)
        var identifierLower = identifier.ToLowerInvariant();
        
        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is { } entity)
            {
                var characterName = Identity.Name(entity, EntityManager);
                var characterNameLower = characterName.ToLowerInvariant();
                
                // Check for exact match
                if (characterNameLower == identifierLower)
                {
                    return player;
                }
                
                // Check for match up to first space
                var firstWord = characterNameLower.Split(' ').FirstOrDefault();
                if (firstWord != null && firstWord == identifierLower)
                {
                    return player;
                }
            }
        }

        return null;
    }
}
