using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.Corporations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Client._WF.Corporations;

public sealed partial class CorporationUiFragment : BoxContainer
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    // Events
    public event Action? OnRefresh;
    public event Action<CorporationView>? OnNavigate;
    public event Action<string, string, CorporationPrivacy>? OnCreate;
    public event Action<int>? OnJoin;
    public event Action? OnLeave;
    public event Action? OnDisband;
    public event Action<string>? OnEditDescription;
    public event Action<CorporationPrivacy>? OnSetPrivacy;
    public event Action<string>? OnSendInvite;
    public event Action<int, bool>? OnRespondInvite;
    public event Action<string>? OnKick;
    public event Action<string, CorporationRank>? OnChangeRank;
    public event Action<string>? OnPurchaseStation;
    public event Action? OnToggleStationVisibility;

    // Controls
    private readonly Button _backButton;
    private readonly Button _refreshButton;
    private readonly Label _feedbackLabel;
    private readonly PanelContainer _upkeepWarningBanner;

    // List panel
    private readonly ScrollContainer _listPanel;
    private readonly BoxContainer _invitesSection;
    private readonly BoxContainer _invitesList;
    private readonly BoxContainer _myCorporationSection;
    private readonly Label _corpNameLabel;
    private readonly Label _corpPrivacyLabel;
    private readonly RichTextLabel _corpDescriptionLabel;
    private readonly Button _editDescriptionButton;
    private readonly OptionButton _privacyOptionButton;
    private readonly Button _inviteMemberButton;
    private readonly BoxContainer _membersList;
    private readonly Label _corpBankBalanceLabel;
    private readonly ConfirmButton _leaveCorpButton;
    private readonly ConfirmButton _disbandCorpButton;
    private readonly BoxContainer _stationExistsBox;
    private readonly Label _stationNameLabel;
    private readonly Label _stationCoordsLabel;
    private readonly Label _stationUpkeepLabel;
    private readonly Label _stationUpkeepWarning;
    private readonly Label _stationSectionLabel;
    private readonly Label _stationPurchaseStatusLabel;
    private readonly BoxContainer _stationPurchaseInputRow;
    private readonly CheckBox _stationVisibleCheckBox;
    private readonly BoxContainer _stationPurchaseBox;
    private readonly LineEdit _stationNameEdit;
    private readonly Button _purchaseStationButton;
    private readonly BoxContainer _noCorporationSection;
    private readonly Button _createCorpButton;
    private readonly BoxContainer _publicCorpsList;

    // Create panel
    private readonly BoxContainer _createPanel;
    private readonly LineEdit _corpNameEdit;
    private readonly TextEdit _corpDescEdit;
    private readonly Label _createDescriptionLimitLabel;
    private readonly Button _privacyToggleButton;
    private readonly Button _foundCorpButton;
    private readonly Button _cancelCreateButton;

    // Invite panel
    private readonly BoxContainer _invitePanel;
    private readonly OptionButton _characterSelector;
    private readonly Button _sendInviteButton;
    private readonly Button _cancelInviteButton;

    // Edit description panel
    private readonly BoxContainer _editDescPanel;
    private readonly TextEdit _editDescText;
    private readonly Label _editDescriptionLimitLabel;
    private readonly Button _saveDescButton;
    private readonly Button _cancelEditDescButton;
    private bool _suppressPrivacySelectionEvent;
    private CorporationPrivacy _editPrivacy = CorporationPrivacy.Public;
    private int _descriptionMaxLength;

    // State 
    private CorporationListUiState? _lastListState;
    private CorporationPrivacy _createPrivacy = CorporationPrivacy.Public;
    private List<string> _inviteCharacters = new();

    // Constructor
    public CorporationUiFragment()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _backButton = FindControl<Button>("BackButton");
        _refreshButton = FindControl<Button>("RefreshButton");
        _feedbackLabel = FindControl<Label>("FeedbackLabel");
        _upkeepWarningBanner = FindControl<PanelContainer>("UpkeepWarningBanner");

        _listPanel = FindControl<ScrollContainer>("ListPanel");
        _invitesSection = FindControl<BoxContainer>("InvitesSection");
        _invitesList = FindControl<BoxContainer>("InvitesList");
        _myCorporationSection = FindControl<BoxContainer>("MyCorporationSection");
        _corpNameLabel = FindControl<Label>("CorpNameLabel");
        _corpPrivacyLabel = FindControl<Label>("CorpPrivacyLabel");
        _corpDescriptionLabel = FindControl<RichTextLabel>("CorpDescriptionLabel");
        _editDescriptionButton = FindControl<Button>("EditDescriptionButton");
        _privacyOptionButton = FindControl<OptionButton>("PrivacyOptionButton");
        _inviteMemberButton = FindControl<Button>("InviteMemberButton");
        _membersList = FindControl<BoxContainer>("MembersList");
        _corpBankBalanceLabel = FindControl<Label>("CorpBankBalanceLabel");
        _leaveCorpButton = FindControl<ConfirmButton>("LeaveCorpButton");
        _disbandCorpButton = FindControl<ConfirmButton>("DisbandCorpButton");
        _stationExistsBox = FindControl<BoxContainer>("StationExistsBox");
        _stationNameLabel = FindControl<Label>("StationNameLabel");
        _stationCoordsLabel = FindControl<Label>("StationCoordsLabel");
        _stationUpkeepLabel = FindControl<Label>("StationUpkeepLabel");
        _stationUpkeepWarning = FindControl<Label>("StationUpkeepWarning");
        _stationSectionLabel = FindControl<Label>("StationSectionLabel");
        _stationPurchaseStatusLabel = FindControl<Label>("StationPurchaseStatusLabel");
        _stationPurchaseInputRow = FindControl<BoxContainer>("StationPurchaseInputRow");
        _stationVisibleCheckBox = FindControl<CheckBox>("StationVisibleCheckBox");
        _stationPurchaseBox = FindControl<BoxContainer>("StationPurchaseBox");
        _stationNameEdit = FindControl<LineEdit>("StationNameEdit");
        _purchaseStationButton = FindControl<Button>("PurchaseStationButton");
        _noCorporationSection = FindControl<BoxContainer>("NoCorporationSection");
        _createCorpButton = FindControl<Button>("CreateCorpButton");
        _publicCorpsList = FindControl<BoxContainer>("PublicCorpsList");

        _createPanel = FindControl<BoxContainer>("CreatePanel");
        _corpNameEdit = FindControl<LineEdit>("CorpNameEdit");
        _corpDescEdit = FindControl<TextEdit>("CorpDescEdit");
        _createDescriptionLimitLabel = FindControl<Label>("CreateDescriptionLimitLabel");
        _privacyToggleButton = FindControl<Button>("PrivacyToggleButton");
        _foundCorpButton = FindControl<Button>("FoundCorpButton");
        _cancelCreateButton = FindControl<Button>("CancelCreateButton");

        _invitePanel = FindControl<BoxContainer>("InvitePanel");
        _characterSelector = FindControl<OptionButton>("CharacterSelector");
        _sendInviteButton = FindControl<Button>("SendInviteButton");
        _cancelInviteButton = FindControl<Button>("CancelInviteButton");

        _editDescPanel = FindControl<BoxContainer>("EditDescPanel");
        _editDescText = FindControl<TextEdit>("EditDescText");
        _editDescriptionLimitLabel = FindControl<Label>("EditDescriptionLimitLabel");
        _saveDescButton = FindControl<Button>("SaveDescButton");
        _cancelEditDescButton = FindControl<Button>("CancelEditDescButton");

        _descriptionMaxLength = _cfg.GetCVar(WFCCVars.CorporationDescriptionMaxLength);

        InitializePrivacyOptionButton();
        UpdateDescriptionLengthDisplay();

        WireEvents();
    }

    private void WireEvents()
    {
        _refreshButton.OnPressed += _ => OnRefresh?.Invoke();
        _backButton.OnPressed += _ =>
        {
            OnNavigate?.Invoke(CorporationView.List);
        };

        // Create flow
        _createCorpButton.OnPressed += _ =>
        {
            _corpNameEdit.Clear();
            _corpDescEdit.TextRope = Rope.Leaf.Empty;
            _corpDescEdit.CursorPosition = default;
            _createPrivacy = CorporationPrivacy.Public;
            UpdatePrivacyButtonText();
            ShowPanel(PanelMode.Create);
        };
        _privacyToggleButton.OnPressed += _ =>
        {
            _createPrivacy = GetNextPrivacy(_createPrivacy);
            UpdatePrivacyButtonText();
        };
        _foundCorpButton.OnPressed += _ =>
        {
            var name = _corpNameEdit.Text.Trim();
            var desc = Rope.Collapse(_corpDescEdit.TextRope).Trim();
            OnCreate?.Invoke(name, desc, _createPrivacy);
        };
        _corpDescEdit.OnTextChanged += _ => UpdateDescriptionLengthDisplay();
        _cancelCreateButton.OnPressed += _ => OnNavigate?.Invoke(CorporationView.List);

        // Corp actions
        _leaveCorpButton.OnPressed += _ => OnLeave?.Invoke();
        _disbandCorpButton.OnPressed += _ => OnDisband?.Invoke();
        // ConfirmButton.OnPressed fires only after the second (confirmed) click
        _inviteMemberButton.OnPressed += _ => OnNavigate?.Invoke(CorporationView.Invite);

        _editDescriptionButton.OnPressed += _ =>
        {
            _editDescText.TextRope = _lastListState?.MyCorporation != null
                ? new Rope.Leaf(_lastListState.MyCorporation.Description)
                : Rope.Leaf.Empty;
            _editDescText.CursorPosition = default;

            if (_lastListState?.MyCorporation != null)
                SetSelectedPrivacyOption(_lastListState.MyCorporation.Privacy);

            ShowPanel(PanelMode.EditDesc);
        };
        _saveDescButton.OnPressed += _ =>
        {
            OnEditDescription?.Invoke(Rope.Collapse(_editDescText.TextRope).Trim());

            if (_lastListState?.MyCorporation != null && _editPrivacy != _lastListState.MyCorporation.Privacy)
                OnSetPrivacy?.Invoke(_editPrivacy);
        };
        _editDescText.OnTextChanged += _ => UpdateDescriptionLengthDisplay();
        _cancelEditDescButton.OnPressed += _ => OnNavigate?.Invoke(CorporationView.List);

        // Invite
        _sendInviteButton.OnPressed += _ =>
        {
            if (_inviteCharacters.Count == 0)
                return;
            var idx = _characterSelector.SelectedId;
            if (idx < 0 || idx >= _inviteCharacters.Count)
                return;
            OnSendInvite?.Invoke(_inviteCharacters[idx]);
        };
        _characterSelector.OnItemSelected += args => _characterSelector.SelectId(args.Id);
        _cancelInviteButton.OnPressed += _ => OnNavigate?.Invoke(CorporationView.List);

        // Station purchase
        _purchaseStationButton.OnPressed += _ =>
        {
            var name = _stationNameEdit.Text.Trim();
            OnPurchaseStation?.Invoke(name);
        };

        // Station visibility toggle
        _stationVisibleCheckBox.OnPressed += _ => OnToggleStationVisibility?.Invoke();
    }

    // State update entry points

    public void ShowListState(CorporationListUiState state)
    {
        _lastListState = state;
        ShowPanel(PanelMode.List);

        // Feedback message
        if (!string.IsNullOrEmpty(state.ErrorMessage))
        {
            _feedbackLabel.Text = Loc.GetString(state.ErrorMessage);
            _feedbackLabel.Visible = true;
        }
        else
        {
            _feedbackLabel.Visible = false;
        }

        // Pending invites
        _invitesList.DisposeAllChildren();
        _invitesList.RemoveAllChildren();

        if (state.PendingInvites.Count > 0)
        {
            _invitesSection.Visible = true;
            foreach (var invite in state.PendingInvites)
                _invitesList.AddChild(BuildInviteRow(invite));
        }
        else
        {
            _invitesSection.Visible = false;
        }

        if (state.MyCorporation != null)
        {
            _myCorporationSection.Visible = true;
            _noCorporationSection.Visible = false;
            var corp = state.MyCorporation;
            _upkeepWarningBanner.Visible = corp.HasStation
                && corp.StationUpkeepCost.HasValue
                && corp.Balance < corp.StationUpkeepCost.Value;
            PopulateMyCorporation(state);
        }
        else
        {
            _myCorporationSection.Visible = false;
            _noCorporationSection.Visible = true;
            _upkeepWarningBanner.Visible = false;
            PopulatePublicCorps(state);
        }
    }

    public void ShowInviteState(CorporationInviteUiState state)
    {
        _inviteCharacters = state.AvailableCharacters;
        _characterSelector.Clear();

        for (var i = 0; i < _inviteCharacters.Count; i++)
            _characterSelector.AddItem(_inviteCharacters[i], i);

        if (!string.IsNullOrEmpty(state.ErrorMessage))
        {
            _feedbackLabel.Text = Loc.GetString(state.ErrorMessage);
            _feedbackLabel.Visible = true;
        }
        else
        {
            _feedbackLabel.Visible = false;
        }

        ShowPanel(PanelMode.Invite);
    }

    // Corp detail population

    private void PopulateMyCorporation(CorporationListUiState state)
    {
        var corp = state.MyCorporation!;
        var myRank = state.MyRank;

        _corpNameLabel.Text = corp.Name;
        _corpPrivacyLabel.Text = Loc.GetString(GetPrivacyLocKey(corp.Privacy));

        _corpDescriptionLabel.Text = string.IsNullOrWhiteSpace(corp.Description)
            ? Loc.GetString("corp-no-description")
            : corp.Description;

        var isManager = myRank >= CorporationRank.Manager;
        var isLeader = myRank == CorporationRank.Leader;
        var isRecruiter = myRank >= CorporationRank.Recruiter;
        var purchaseEnabled = state.StationPurchaseEnabled;

        _editDescriptionButton.Visible = isManager;
        _inviteMemberButton.Visible = isRecruiter;
        _disbandCorpButton.Visible = isLeader;
        _disbandCorpButton.Disabled = corp.Balance > 0;

        _corpBankBalanceLabel.Text = Loc.GetString("corp-bank-balance", ("balance", corp.Balance.ToString("N0")));

        // Build members list
        _membersList.DisposeAllChildren();
        _membersList.RemoveAllChildren();

        var sorted = state.Members
            .OrderByDescending(m => m.Rank)
            .ThenBy(m => m.DisplayName)
            .ToList();

        foreach (var member in sorted)
            _membersList.AddChild(BuildMemberRow(member, myRank, state.MyUserId));

        // Station section
        _stationSectionLabel.Text = !purchaseEnabled && !corp.HasStation
            ? Loc.GetString("corp-section-station-unavailable")
            : Loc.GetString("corp-section-station");

        _stationExistsBox.Visible = corp.HasStation;
        _stationPurchaseBox.Visible = isManager && !corp.HasStation;

        if (!corp.HasStation)
        {
            if (!purchaseEnabled)
            {
                _stationPurchaseStatusLabel.Text = Loc.GetString("corp-section-station-unavailable");
                _stationPurchaseInputRow.Visible = false;
                _purchaseStationButton.Visible = false;
            }
            else
            {
                _stationPurchaseStatusLabel.Text = Loc.GetString("corp-station-none");
                _stationPurchaseInputRow.Visible = true;
                _purchaseStationButton.Visible = true;
            }
        }

        if (corp.HasStation && corp.StationName != null)
        {
            _stationNameLabel.Text = corp.StationName;
            _stationVisibleCheckBox.Pressed = corp.StationVisible;
            _stationVisibleCheckBox.Disabled = !isManager;
            if (corp.StationCoordinates is { } coords)
                _stationCoordsLabel.Text = Loc.GetString("corp-station-coords",
                    ("x", (int)coords.X), ("y", (int)coords.Y));
            else
                _stationCoordsLabel.Text = string.Empty;

            if (corp.StationUpkeepCost is { } upkeep)
            {
                _stationUpkeepLabel.Text = Loc.GetString("corp-station-upkeep-cost", ("amount", upkeep.ToString("N0")));
                var canAfford = corp.Balance >= upkeep;
                _stationUpkeepWarning.Visible = !canAfford;
                _stationUpkeepWarning.Text = Loc.GetString("corp-station-upkeep-warning");
            }
            else
            {
                _stationUpkeepLabel.Text = string.Empty;
                _stationUpkeepWarning.Visible = false;
            }
        }
    }

    private void PopulatePublicCorps(CorporationListUiState state)
    {
        _publicCorpsList.DisposeAllChildren();
        _publicCorpsList.RemoveAllChildren();

        if (state.PublicCorporations.Count == 0)
        {
            _publicCorpsList.AddChild(new Label { Text = Loc.GetString("corp-no-public-corps") });
            return;
        }

        foreach (var corp in state.PublicCorporations.OrderBy(c => c.Name))
            _publicCorpsList.AddChild(BuildPublicCorpRow(corp));
    }

    // Row builders

    private Control BuildInviteRow(CorporationInfo corp)
    {
        var panel = new PanelContainer
        {
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalExpand = true,
        };

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        var nameLabel = new Label
        {
            Text = Loc.GetString("corp-invite-row", ("name", corp.Name), ("members", corp.MemberCount)),
            HorizontalExpand = true,
        };

        var acceptBtn = new Button { Text = Loc.GetString("corp-btn-accept"), Margin = new Thickness(4, 0, 0, 0) };
        var declineBtn = new Button { Text = Loc.GetString("corp-btn-decline"), Margin = new Thickness(4, 0, 0, 0) };

        var capturedCorpId = corp.Id;
        acceptBtn.OnPressed += _ => OnRespondInvite?.Invoke(capturedCorpId, true);
        declineBtn.OnPressed += _ => OnRespondInvite?.Invoke(capturedCorpId, false);

        row.AddChild(nameLabel);
        row.AddChild(acceptBtn);
        row.AddChild(declineBtn);
        panel.AddChild(row);
        return panel;
    }

    private Control BuildPublicCorpRow(CorporationInfo corp)
    {
        var panel = new PanelContainer
        {
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalExpand = true,
            StyleClasses = { "AngleRect" },
            ModulateSelfOverride = Color.FromHex("#3F3F3F"),
        };

        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(4, 4, 4, 4),
        };

        var headerRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };

        var nameLabel = new Label
        {
            Text = corp.Name,
            StyleClasses = { "LabelSubText" },
            HorizontalExpand = true,
        };

        var memberCount = new Label
        {
            Text = Loc.GetString("corp-member-count", ("count", corp.MemberCount)),
            Margin = new Thickness(8, 0, 0, 0),
        };

        var joinBtn = new Button
        {
            Text = corp.Privacy == CorporationPrivacy.Private
                ? Loc.GetString("corp-btn-request-invite")
                : Loc.GetString("corp-btn-join"),
            Margin = new Thickness(8, 0, 0, 0),
            Disabled = corp.Privacy == CorporationPrivacy.Private,
        };

        var capturedCorpId = corp.Id;
        if (corp.Privacy == CorporationPrivacy.Public)
            joinBtn.OnPressed += _ => OnJoin?.Invoke(capturedCorpId);

        headerRow.AddChild(nameLabel);
        headerRow.AddChild(memberCount);
        headerRow.AddChild(joinBtn);
        container.AddChild(headerRow);

        if (!string.IsNullOrWhiteSpace(corp.Description))
        {
            var descLabel = new RichTextLabel
            {
                HorizontalExpand = true,
                Margin = new Thickness(0, 2, 0, 0),
            };
            descLabel.StyleClasses.Add("LabelSmall");
            descLabel.SetMessage(FormattedMessage.FromMarkupPermissive(corp.Description));
            container.AddChild(descLabel);
        }

        panel.AddChild(container);
        return panel;
    }

    private Control BuildMemberRow(CorporationMemberInfo member, CorporationRank myRank, string myUserId)
    {
        var isSelf = member.UserId == myUserId;
        var memberRank = member.Rank;

        var panel = new PanelContainer
        {
            Margin = new Thickness(0, 0, 0, 2),
            HorizontalExpand = true,
        };

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        var rankLabel = new Label
        {
            Text = $"[{Loc.GetString($"corp-rank-{member.Rank.ToString().ToLowerInvariant()}")}]",
            MinWidth = 100,
            StyleClasses = { "LabelSmall" },
        };

        var nameLabel = new Label
        {
            Text = member.DisplayName + (isSelf ? Loc.GetString("corp-member-self-suffix") : ""),
            HorizontalExpand = true,
            StyleClasses = { "LabelSmall" },
        };

        row.AddChild(rankLabel);
        row.AddChild(nameLabel);

        // Management actions - only for non-self and when I outrank them
        if (!isSelf && myRank > memberRank)
        {
            var capturedUserId = member.UserId;

            // Rank dropdown — shows all ranks the current user is allowed to assign to this member.
            // Assignable range: Member (0) up to one below my own rank.
            var rankDropdown = new OptionButton
            {
                Margin = new Thickness(4, 0, 0, 0),
                StyleClasses = { "ButtonSmall" },
            };

            int selectedIdx = 0;
            int idx = 0;
            for (var r = CorporationRank.Member; r < myRank; r++)
            {
                var rankKey = $"corp-rank-{r.ToString().ToLowerInvariant()}";
                rankDropdown.AddItem(Loc.GetString(rankKey), (int) r);
                if (r == memberRank)
                    selectedIdx = idx;
                idx++;
            }
            rankDropdown.SelectId((int) memberRank);

            rankDropdown.OnItemSelected += args =>
            {
                rankDropdown.SelectId(args.Id);
                var newRank = (CorporationRank) args.Id;
                OnChangeRank?.Invoke(capturedUserId, newRank);
            };

            row.AddChild(rankDropdown);

            // Kick button
            var kickBtn = new ConfirmButton
            {
                Text = Loc.GetString("corp-btn-kick"),
                ConfirmationText = Loc.GetString("corp-btn-kick-confirm"),
                Margin = new Thickness(4, 0, 0, 0),
                StyleClasses = { "ButtonSmall" },
            };
            kickBtn.OnPressed += _ => OnKick?.Invoke(capturedUserId);
            row.AddChild(kickBtn);
        }

        panel.AddChild(row);
        return panel;
    }

    // Panel switching

    private enum PanelMode { List, Create, Invite, EditDesc }

    private void ShowPanel(PanelMode mode)
    {
        _listPanel.Visible = mode == PanelMode.List;
        _createPanel.Visible = mode == PanelMode.Create;
        _invitePanel.Visible = mode == PanelMode.Invite;
        _editDescPanel.Visible = mode == PanelMode.EditDesc;

        _backButton.Visible = mode != PanelMode.List;
    }

    // Helpers

    private void UpdatePrivacyButtonText()
    {
        _privacyToggleButton.Text = Loc.GetString(GetPrivacyLocKey(_createPrivacy));
    }

    private void InitializePrivacyOptionButton()
    {
        foreach (var privacy in Enum.GetValues<CorporationPrivacy>())
        {
            _privacyOptionButton.AddItem(Loc.GetString(GetPrivacyLocKey(privacy)), (int) privacy);
        }

        _privacyOptionButton.OnItemSelected += args =>
        {
            _privacyOptionButton.SelectId(args.Id);

            if (_suppressPrivacySelectionEvent)
                return;

            _editPrivacy = (CorporationPrivacy) args.Id;
        };
    }

    private void SetSelectedPrivacyOption(CorporationPrivacy privacy)
    {
        _editPrivacy = privacy;
        _suppressPrivacySelectionEvent = true;
        _privacyOptionButton.SelectId((int) privacy);
        _suppressPrivacySelectionEvent = false;
    }

    private void UpdateDescriptionLengthDisplay()
    {
        var createLength = _corpDescEdit.TextLength;
        var createOverMax = createLength > _descriptionMaxLength;
        _createDescriptionLimitLabel.Text = Loc.GetString("corp-description-limit", ("current", createLength), ("max", _descriptionMaxLength));
        _createDescriptionLimitLabel.FontColorOverride = createOverMax ? Color.Red : Color.FromHex("#AAAAAA");
        _foundCorpButton.Disabled = createOverMax;

        var editLength = _editDescText.TextLength;
        var editOverMax = editLength > _descriptionMaxLength;
        _editDescriptionLimitLabel.Text = Loc.GetString("corp-description-limit", ("current", editLength), ("max", _descriptionMaxLength));
        _editDescriptionLimitLabel.FontColorOverride = editOverMax ? Color.Red : Color.FromHex("#AAAAAA");
        _saveDescButton.Disabled = editOverMax;
    }

    private static CorporationPrivacy GetNextPrivacy(CorporationPrivacy privacy)
    {
        return privacy switch
        {
            CorporationPrivacy.Public => CorporationPrivacy.Private,
            CorporationPrivacy.Private => CorporationPrivacy.Unlisted,
            _ => CorporationPrivacy.Public,
        };
    }

    private static string GetPrivacyLocKey(CorporationPrivacy privacy)
    {
        return privacy switch
        {
            CorporationPrivacy.Public => "corp-privacy-public",
            CorporationPrivacy.Private => "corp-privacy-private",
            _ => "corp-privacy-unlisted",
        };
    }
}
