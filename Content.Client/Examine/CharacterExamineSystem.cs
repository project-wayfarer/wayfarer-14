using Content.Client.Examine.UI;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Content.Shared._WF.RoleplayLeveling.Events; // Wayfarer
using Content.Client._WF.RoleplayLeveling.UI; // Wayfarer
using Robust.Client.GameObjects;
using Robust.Client.Player; // Wayfarer
using Robust.Client.UserInterface;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton; // Wayfarer

namespace Content.Client.Examine;

/// <summary>
/// Adds a "Character" examine button to humanoid entities and ghost roles that opens a character info window
/// </summary>
public sealed class CharacterExamineSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystem _examine = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!; // Wayfarer

    private readonly Dictionary<NetEntity, CharacterDetailWindow> _openWindows = new();
    private readonly HashSet<NetEntity> _selfWindows = new(); // Wayfarer: windows for own character
    private MyCommendsWindow? _myCommendsWindow; // Wayfarer
    private int _cachedTotalCommends = 0; // Wayfarer

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
        SubscribeLocalEvent<MindContainerComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbsWithMind);
        SubscribeNetworkEvent<CharacterInfoEvent>(HandleCharacterInfo);
        SubscribeNetworkEvent<AvailableCommendsMessage>(HandleAvailableCommends); // Wayfarer
        SubscribeNetworkEvent<MyCommendsMessage>(HandleMyCommends); // Wayfarer
    }

    private void OnGetExamineVerbs(EntityUid uid, HumanoidAppearanceComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        // Wayfarer Begin
        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("character-examine-verb"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/sentient.svg.192dpi.png")),
            Act = () => OpenCharacterWindow(uid),
            Category = VerbCategory.Examine,
            ClientExclusive = true,
            ShowOnExamineTooltip = true,
        });
        // End Wayfarer
    }

    private void OnGetExamineVerbsWithMind(EntityUid uid, MindContainerComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        // Only add if not already a humanoid (to avoid duplicate buttons)
        if (HasComp<HumanoidAppearanceComponent>(uid))
            return;

        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("character-examine-verb"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/sentient.svg.192dpi.png")),
            Act = () => OpenCharacterWindow(uid),
            Category = VerbCategory.Examine,
            ClientExclusive = true,
            ShowOnExamineTooltip = true,
        });
    }

    private void OpenCharacterWindow(EntityUid uid)
    {
        var netEntity = GetNetEntity(uid);

        // Close existing window for this entity if it exists
        if (_openWindows.TryGetValue(netEntity, out var existingWindow))
        {
            existingWindow.Close();
            _openWindows.Remove(netEntity);
        }

        // Create and show new window
        var window = new CharacterDetailWindow();
        _openWindows[netEntity] = window;

        window.OnClose += () =>
        {
            _openWindows.Remove(netEntity);
            _selfWindows.Remove(netEntity);
        };

        // Wayfarer: detect if opening your own character window
        var isSelf = _player.LocalEntity == uid;
        if (isSelf)
        {
            _selfWindows.Add(netEntity);
            window.CommendFormSection.Visible = false;
            window.SelfCommendSection.Visible = true;
            window.ViewMyCommendsButton.OnPressed += _ => OnViewMyCommends();
        }
        else
        {
            // Wire up commend button for other players
            window.SubmitCommendButton.OnPressed += args => OnSubmitCommend(args, uid, window);
        }
        // End Wayfarer

        window.OpenCentered();

        // Request character info from server
        RaiseNetworkEvent(new RequestCharacterInfoEvent { Entity = netEntity });

        // Wayfarer
        if (isSelf)
        {
            // No need to request available commends for self view
        }
        else
        {
            // Request available commends count for giving
            RaiseNetworkEvent(new RequestAvailableCommendsMessage());
        }
        // End Wayfarer
    }

    private void HandleCharacterInfo(CharacterInfoEvent message)
    {
        if (!_openWindows.TryGetValue(message.Entity, out var window))
            return;

        // Set character info
        window.SetCharacterInfo(message.CharacterName, message.RoleplayLevel); // Wayfarer: message.JobTitle<message.RoleplayLevel

        // Wayfarer: update self commends label if this is self window
        if (_selfWindows.Contains(message.Entity))
        {
            _cachedTotalCommends = message.TotalCommends;
            var count = message.TotalCommends;
            window.SelfTotalCommendsLabel.Text = count == 1
                ? "You have received 1 commend."
                : $"You have received {count} commends.";
        }
        // End Wayfarer

        // Set description with markup parsing
        FormattedMessage descriptionMessage;
        if (!string.IsNullOrWhiteSpace(message.Description))
        {
            descriptionMessage = FormattedMessage.FromMarkupPermissive(message.Description);
        }
        else
        {
            descriptionMessage = new FormattedMessage();
            descriptionMessage.AddText(Loc.GetString("character-window-no-description"));
        }
        window.SetDescription(descriptionMessage);

        // Set consent text with markup parsing
        FormattedMessage consentMessage;
        if (!string.IsNullOrWhiteSpace(message.ConsentText))
        {
            consentMessage = FormattedMessage.FromMarkupPermissive(message.ConsentText);
        }
        else
        {
            consentMessage = new FormattedMessage();
            consentMessage.AddText(Loc.GetString("character-window-no-consent"));
        }
        window.SetConsent(consentMessage);
    }

    // Wayfarer
    private void OnSubmitCommend(ButtonEventArgs args, EntityUid targetEntity, CharacterDetailWindow window)
    {
        // Prevent self-commending
        if (_player.LocalEntity == targetEntity)
        {
            window.SubmitCommendButton.Text = "Cannot commend yourself!";
            return;
        }

        var comment = Rope.Collapse(window.CommendCommentInput.TextRope);
        if (string.IsNullOrWhiteSpace(comment))
        {
            window.SubmitCommendButton.Text = "Please enter a comment!";
            return;
        }

        var isPrivate = window.CommendPrivateCheckbox.Pressed;

        // Send the commend message
        RaiseNetworkEvent(new GiveCommendMessage(GetNetEntity(targetEntity), comment, isPrivate));

        // Clear the form and show success
        window.CommendCommentInput.TextRope = new Rope.Leaf("");
        window.CommendPrivateCheckbox.Pressed = false;
        window.SubmitCommendButton.Text = "Commend sent!";
    }
    // End Wayfarer

    private void HandleAvailableCommends(AvailableCommendsMessage message)
    {
        // Update only non-self windows with the new commends count
        foreach (var (netEntity, window) in _openWindows)
        {
            if (_selfWindows.Contains(netEntity))
                continue;
            var hasCommends = message.AvailableCommends > 0;

            // Update text with appropriate pluralization
            var commendsText = message.AvailableCommends == 1
                ? "You have 1 commend left to give"
                : $"You have {message.AvailableCommends} commends left to give";
            window.CommendsRemainingLabel.Text = commendsText;

            // Enable/disable UI elements based on available commends
            window.CommendCommentInput.Editable = hasCommends;
            window.CommendPrivateCheckbox.Disabled = !hasCommends;
            window.SubmitCommendButton.Disabled = !hasCommends;

            // Update button text if no commends available
            if (!hasCommends)
            {
                window.SubmitCommendButton.Text = "No Commends Available";
            }
            else if (window.SubmitCommendButton.Text == "No Commends Available" ||
                     window.SubmitCommendButton.Text == "Commend sent!")
            {
                window.SubmitCommendButton.Text = "Submit Commend";
            }
        }
    }

    // Wayfarer
    private void OnViewMyCommends()
    {
        // Close any existing window first
        _myCommendsWindow?.Close();
        _myCommendsWindow = new MyCommendsWindow();
        _myCommendsWindow.OpenCentered();

        // Request commends from server
        RaiseNetworkEvent(new RequestMyCommendsMessage());
    }

    private void HandleMyCommends(MyCommendsMessage message)
    {
        if (_myCommendsWindow == null || !_myCommendsWindow.IsOpen)
        {
            _myCommendsWindow = new MyCommendsWindow();
            _myCommendsWindow.OpenCentered();
        }

        _myCommendsWindow.Populate(message.Commends, _cachedTotalCommends);
    }
    // End Wayfarer
}
