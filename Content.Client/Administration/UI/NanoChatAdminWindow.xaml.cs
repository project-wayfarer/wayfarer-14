using System.Linq;
using System.Numerics;
using Content.Shared.Administration;
using Content.Shared._DeltaV.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Administration.UI;

public sealed class NanoChatAdminWindow : DefaultWindow
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private NanoChatAdminEuiState? _state;
    private NanoChatCardData? _selectedCard;
    private uint? _selectedRecipient;

    // UI Controls
    private Button RefreshButton => (Button)FindControl("RefreshButton")!;
    private LineEdit SearchLineEdit => (LineEdit)FindControl("SearchLineEdit")!;
    private SplitContainer MainSplit => (SplitContainer)FindControl("MainSplit")!;
    private BoxContainer CardList => (BoxContainer)FindControl("CardList")!;
    private BoxContainer CardInfoPanel => (BoxContainer)FindControl("CardInfoPanel")!;
    private Label CardOwnerLabel => (Label)FindControl("CardOwnerLabel")!;
    private Label CardUsernameLabel => (Label)FindControl("CardUsernameLabel")!;
    private Label CardNumberLabel => (Label)FindControl("CardNumberLabel")!;
    private BoxContainer RecipientSelectorPanel => (BoxContainer)FindControl("RecipientSelectorPanel")!;
    private BoxContainer RecipientList => (BoxContainer)FindControl("RecipientList")!;
    private Control RecipientSeparator => FindControl("RecipientSeparator")!;
    private BoxContainer MessagesPanel => (BoxContainer)FindControl("MessagesPanel")!;
    private Label ConversationLabel => (Label)FindControl("ConversationLabel")!;
    private ScrollContainer MessagesScroll => (ScrollContainer)FindControl("MessagesScroll")!;
    private BoxContainer MessageList => (BoxContainer)FindControl("MessageList")!;
    private BoxContainer NoSelectionPanel => (BoxContainer)FindControl("NoSelectionPanel")!;

    public event Action? OnRefreshPressed;
    public event Action<NetEntity>? OnCardSelected;

    public NanoChatAdminWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        RefreshButton.OnPressed += _ => OnRefreshPressed?.Invoke();
        SearchLineEdit.OnTextChanged += _ => FilterCardList();

        MainSplit.State = SplitContainer.SplitState.Auto;
        MainSplit.ResizeMode = SplitContainer.SplitResizeMode.RespectChildrenMinSize;
    }

    private Control FindControl(string name)
    {
        return this.FindControl<Control>(name) ?? throw new InvalidOperationException($"Control '{name}' not found");
    }

    public void UpdateState(NanoChatAdminEuiState state)
    {
        _state = state;
        
        // Store the currently selected card entity and recipient for re-selection
        var previousCardEntity = _selectedCard?.CardEntity;
        var previousRecipientNumber = _selectedRecipient;
        
        FilterCardList();
        
        // Re-select the previously selected card and conversation if they still exist
        if (previousCardEntity != null)
        {
            var card = _state.Cards.FirstOrDefault(c => c.CardEntity == previousCardEntity);
            if (card != null)
            {
                SelectCard(card);
                
                // Re-select the conversation if it was previously selected
                if (previousRecipientNumber != null && card.Recipients.ContainsKey(previousRecipientNumber.Value))
                {
                    var recipient = card.Recipients[previousRecipientNumber.Value];
                    SelectRecipient(previousRecipientNumber.Value, recipient);
                }
            }
        }
    }

    private void FilterCardList()
    {
        CardList.RemoveAllChildren();

        if (_state == null)
            return;

        var searchText = SearchLineEdit.Text.Trim().ToLower();
        var filteredCards = _state.Cards.Where(card =>
            string.IsNullOrEmpty(searchText) ||
            card.OwnerName.ToLower().Contains(searchText) ||
            (card.JobTitle?.ToLower().Contains(searchText) ?? false) ||
            (card.Number?.ToString().Contains(searchText) ?? false)
        );

        foreach (var card in filteredCards)
        {
            var button = new Button
            {
                Text = $"{card.OwnerName} ({card.JobTitle ?? "No Job"}) - #{card.Number?.ToString("D4") ?? "No Number"}",
                StyleClasses = { "OpenLeft" },
                HorizontalAlignment = Control.HAlignment.Stretch,
                VerticalAlignment = Control.VAlignment.Top,
                MinHeight = 28,
                MinWidth = 200,
                ClipText = true
            };

            button.OnPressed += _ => SelectCard(card);
            CardList.AddChild(button);
            
            // Add spacing between buttons
            CardList.AddChild(new Control { MinHeight = 4 });
        }
    }

    private void SelectCard(NanoChatCardData card)
    {
        _selectedCard = card;
        _selectedRecipient = null;

        OnCardSelected?.Invoke(card.CardEntity);

        // Update card info
        CardOwnerLabel.Text = $"{card.OwnerName} ({card.JobTitle ?? "No Job"})";
        CardUsernameLabel.Text = card.Username != null ? $"Player: {card.Username}" : "";
        CardUsernameLabel.Visible = !string.IsNullOrEmpty(card.Username);
        CardNumberLabel.Text = $"NanoChat Number: #{card.Number?.ToString("D4") ?? "Not Assigned"}";
        
        CardInfoPanel.Visible = true;
        NoSelectionPanel.Visible = false;

        // Show recipient list
        PopulateRecipientList(card);
    }

    private void PopulateRecipientList(NanoChatCardData card)
    {
        RecipientList.RemoveAllChildren();

        if (card.Recipients.Count == 0)
        {
            RecipientSelectorPanel.Visible = false;
            RecipientSeparator.Visible = false;
            MessagesPanel.Visible = false;
            
            var noChatsLabel = new Label
            {
                Text = Loc.GetString("nanochat-admin-no-conversations"),
                StyleClasses = { "LabelSubText" }
            };
            RecipientList.AddChild(noChatsLabel);
            RecipientSelectorPanel.Visible = true;
            
            // Debug: Show message count
            var debugLabel = new Label
            {
                Text = $"(Card has {card.Messages.Count} message groups)",
                StyleClasses = { "LabelSubText" },
                FontColorOverride = Color.Gray
            };
            RecipientList.AddChild(debugLabel);
            return;
        }

        RecipientSelectorPanel.Visible = true;
        RecipientSeparator.Visible = true;
        MessagesPanel.Visible = false; // Hide messages until a conversation is selected

        // Sort recipients by name
        var sortedRecipients = card.Recipients.OrderBy(r => r.Value.Name);

        foreach (var (number, recipient) in sortedRecipients)
        {
            var messageCount = card.Messages.TryGetValue(number, out var messages) ? messages.Count : 0;
            
            var button = new Button
            {
                Text = $"{recipient.Name} ({recipient.JobTitle ?? "No Job"}) - #{number:D4} - {messageCount} messages",
                StyleClasses = { "OpenLeft" },
                HorizontalAlignment = Control.HAlignment.Stretch,
                VerticalAlignment = Control.VAlignment.Top,
                MinHeight = 28,
                MinWidth = 200,
                ClipText = true
            };

            button.OnPressed += _ => SelectRecipient(number, recipient);
            RecipientList.AddChild(button);
            
            // Add spacing between buttons
            RecipientList.AddChild(new Control { MinHeight = 4 });
        }
    }

    private void SelectRecipient(uint recipientNumber, NanoChatRecipient recipient)
    {
        _selectedRecipient = recipientNumber;
        
        ConversationLabel.Text = Loc.GetString("nanochat-admin-conversation-with", 
            ("name", recipient.Name), 
            ("job", recipient.JobTitle ?? "No Job"));

        DisplayMessages();
    }

    private void DisplayMessages()
    {
        MessageList.RemoveAllChildren();

        if (_selectedCard == null || _selectedRecipient == null)
        {
            MessagesPanel.Visible = false;
            return;
        }

        MessagesPanel.Visible = true;

        if (!_selectedCard.Messages.TryGetValue(_selectedRecipient.Value, out var messages) || messages.Count == 0)
        {
            var noMessagesLabel = new Label
            {
                Text = Loc.GetString("nanochat-admin-no-messages"),
                StyleClasses = { "LabelSubText" }
            };
            MessageList.AddChild(noMessagesLabel);
            return;
        }

        // Sort messages by timestamp
        var sortedMessages = messages.OrderBy(m => m.Timestamp);

        foreach (var message in sortedMessages)
        {
            var isFromOwner = message.SenderId == _selectedCard.Number;
            var senderName = isFromOwner ? _selectedCard.OwnerName : 
                (_selectedCard.Recipients.TryGetValue(message.SenderId, out var sender) ? sender.Name : "Unknown");

            var messageContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(0, 0, 0, 8)
            };

            // Message header with sender and timestamp
            var headerContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal
            };

            var senderLabel = new Label
            {
                Text = $"{senderName}:",
                StyleClasses = { "LabelHeading" },
                FontColorOverride = isFromOwner ? Color.LightBlue : Color.LightGreen
            };

            var timestampLabel = new Label
            {
                Text = $" ({FormatTimestamp(message.Timestamp)})",
                StyleClasses = { "LabelSubText" }
            };

            headerContainer.AddChild(senderLabel);
            headerContainer.AddChild(timestampLabel);
            
            // Add username label if available from the message
            if (!string.IsNullOrEmpty(message.SenderUsername))
            {
                var usernameLabel = new Label
                {
                    Text = $"  @{message.SenderUsername}",
                    StyleClasses = { "LabelSubText" },
                    FontColorOverride = Color.Gray
                };
                headerContainer.AddChild(usernameLabel);
            }

            // Check if message was sent by someone other than the card owner (stolen PDA)
            if (isFromOwner && !string.IsNullOrEmpty(message.SenderUsername) && !string.IsNullOrEmpty(_selectedCard.OriginalOwnerUsername))
            {
                // If the sender username doesn't match the original card owner's username, it was stolen
                if (message.SenderUsername != _selectedCard.OriginalOwnerUsername)
                {
                    var stolenLabel = new Label
                    {
                        Text = " [STOLEN]",
                        FontColorOverride = Color.Red,
                        StyleClasses = { "LabelHeading" }
                    };
                    headerContainer.AddChild(stolenLabel);
                }
            }

            if (message.DeliveryFailed)
            {
                var failedLabel = new Label
                {
                    Text = " [FAILED]",
                    FontColorOverride = Color.Red
                };
                headerContainer.AddChild(failedLabel);
            }

            messageContainer.AddChild(headerContainer);

            // Message content
            var contentLabel = new RichTextLabel
            {
                MaxWidth = 600,
                HorizontalExpand = true
            };
            contentLabel.SetMessage(message.Content);
            messageContainer.AddChild(contentLabel);

            MessageList.AddChild(messageContainer);
        }

        // Scroll to bottom
        MessagesScroll.SetScrollValue(new Vector2(0, float.MaxValue));
    }

    private string FormatTimestamp(TimeSpan timestamp)
    {
        var totalSeconds = (int)timestamp.TotalSeconds;
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;
        
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }
}
