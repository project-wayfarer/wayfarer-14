using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Network;

namespace Content.Client.Chat.Systems;

/// <summary>
/// Handles receiving and displaying private messages on the client
/// </summary>
public sealed class PrivateMessageSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    /// <summary>
    /// Tracks the last person who sent a private message for /reply
    /// </summary>
    private NetUserId? _lastPrivateMessageSender;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PrivateMessageEvent>(OnPrivateMessageReceived);
    }

    private void OnPrivateMessageReceived(PrivateMessageEvent ev)
    {
        // Track sender for /reply command
        _lastPrivateMessageSender = ev.SenderUserId;

        // Format the message - escape the message content to prevent markup issues
        var senderDisplay = ev.SenderCharacterName != null 
            ? $"{ev.SenderUsername} ({ev.SenderCharacterName})" 
            : ev.SenderUsername;

        var message = $"[PM from {senderDisplay}]: {ev.Message}";
        var wrappedMessage = $"[color=#9933ff][bold]PM from {senderDisplay}:[/bold] {Robust.Shared.Utility.FormattedMessage.EscapeText(ev.Message)}[/color]";

        // Create a chat message to display
        var chatMessage = new ChatMessage(
            ChatChannel.OOC, // Use OOC channel for PMs so they show up properly
            message,
            wrappedMessage,
            default, // No entity
            null, // No sender key
            false, // Don't hide
            Color.FromHex("#9933ff") // Purple color for PMs
        );

        // Display in chat using the UI controller
        var chatUI = _uiManager.GetUIController<ChatUIController>();
        chatUI.ProcessChatMessage(chatMessage, speechBubble: false);

        // Could also play a sound here if desired
        // _audio.PlayGlobal("/Audio/Effects/pm_notification.ogg", Filter.Local(), false);
    }

    public NetUserId? GetLastSender()
    {
        return _lastPrivateMessageSender;
    }
}
