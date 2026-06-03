using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._WF.Corporations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._WF.Corporations.AdminEui;

public sealed class CorpAdminWindow : DefaultWindow
{
    // Events

    public event Action? OnRefresh;
    public event Action<int, int>? OnSetBalance;
    public event Action<int, string>? OnSetDescription;
    public event Action<int, CorporationPrivacy>? OnSetPrivacy;
    public event Action<int, string>? OnKickMember;
    public event Action<int, string, CorporationRank>? OnSetMemberRank;
    public event Action<int>? OnDeleteCorporation;
    public event Action<int>? OnEvictStation;
    public event Action<int>? OnSaveStation;
    public event Action<int, string>? OnGrantStation;
    public event Action<string, string, CorporationPrivacy>? OnCreateCorporation;
    public event Action<int, Guid>? OnAddMember;
    public event Action<int, string, string>? OnRecoverStation; // corpId, archiveFileName, stationName

    // Layout

    private readonly BoxContainer _corpList;
    private readonly BoxContainer _detailPanel;
    private List<CorpAdminCorpData> _corporations = new();
    private int? _selectedCorpId;

    public CorpAdminWindow()
    {
        Title = "Corporation Admin";
        MinSize = new Vector2(900, 600);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        // Left: corp list
        var leftPanel = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 220,
            Margin = new Thickness(4),
        };

        var refreshBtn = new Button { Text = "Refresh", Margin = new Thickness(0, 0, 0, 2) };
        refreshBtn.OnPressed += _ => OnRefresh?.Invoke();
        leftPanel.AddChild(refreshBtn);

        var openCreateBtn = new Button { Text = "+ Create Corp", Margin = new Thickness(0, 0, 0, 4) };
        openCreateBtn.OnPressed += _ =>
        {
            var dialog = new CorpCreateDialog();
            dialog.OnConfirm += (name, desc, privacy) => OnCreateCorporation?.Invoke(name, desc, privacy);
            dialog.OpenCentered();
        };
        leftPanel.AddChild(openCreateBtn);

        var listScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _corpList = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        listScroll.AddChild(_corpList);
        leftPanel.AddChild(listScroll);

        // Right: detail panel
        var detailScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
        };
        _detailPanel = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(8),
        };
        detailScroll.AddChild(_detailPanel);

        // Separator
        var separator = new PanelContainer { MinWidth = 1 };

        root.AddChild(leftPanel);
        root.AddChild(separator);
        root.AddChild(detailScroll);

        Contents.AddChild(root);
    }

    // State update

    public void UpdateState(CorpAdminEuiState state)
    {
        _corporations = state.Corporations;

        RebuildCorpList();

        // Re-select if the selected corp still exists.
        if (_selectedCorpId.HasValue && _corporations.Any(c => c.Id == _selectedCorpId.Value))
            ShowDetail(_selectedCorpId.Value);
        else
            ClearDetail();
    }

    // Corp list

    private void RebuildCorpList()
    {
        _corpList.DisposeAllChildren();
        _corpList.RemoveAllChildren();

        foreach (var corp in _corporations)
        {
            var captured = corp;
            var btn = new Button
            {
                Text = $"[{corp.Id}] {corp.Name}",
                HorizontalExpand = true,
            };
            btn.OnPressed += _ =>
            {
                _selectedCorpId = captured.Id;
                ShowDetail(captured.Id);
            };
            _corpList.AddChild(btn);
        }
    }

    // Detail panel

    private void ClearDetail()
    {
        _detailPanel.DisposeAllChildren();
        _detailPanel.RemoveAllChildren();
        _detailPanel.AddChild(new Label { Text = "Select a corporation on the left." });
    }

    private void ShowDetail(int corpId)
    {
        var corp = _corporations.FirstOrDefault(c => c.Id == corpId);
        if (corp == null) { ClearDetail(); return; }

        _detailPanel.DisposeAllChildren();
        _detailPanel.RemoveAllChildren();

        // Header
        _detailPanel.AddChild(new Label
        {
            Text = $"[{corp.Id}] {corp.Name}",
            StyleClasses = { "LabelBig" },
            Margin = new Thickness(0, 0, 0, 8),
        });

        // Balance
        AddSectionHeader("Balance");
        var balanceRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var balanceEdit = new LineEdit { Text = corp.Balance.ToString(), MinWidth = 120, Margin = new Thickness(0, 0, 4, 0) };
        var setBalanceBtn = new Button { Text = "Set" };
        setBalanceBtn.OnPressed += _ =>
        {
            if (int.TryParse(balanceEdit.Text.Trim(), out var newBalance))
                OnSetBalance?.Invoke(corpId, newBalance);
        };
        balanceRow.AddChild(new Label { Text = "Spesos: ", VerticalAlignment = VAlignment.Center });
        balanceRow.AddChild(balanceEdit);
        balanceRow.AddChild(setBalanceBtn);
        _detailPanel.AddChild(balanceRow);

        // Description
        AddSectionHeader("Description");
        var descEdit = new LineEdit { Text = corp.Description, HorizontalExpand = true, Margin = new Thickness(0, 0, 0, 4) };
        var setDescRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var setDescBtn = new Button { Text = "Set" };
        setDescBtn.OnPressed += _ => OnSetDescription?.Invoke(corpId, descEdit.Text.Trim());
        setDescRow.AddChild(descEdit);
        setDescRow.AddChild(setDescBtn);
        _detailPanel.AddChild(setDescRow);

        // Privacy
        AddSectionHeader("Privacy");
        var privacyRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var privacyBtn = new OptionButton { Margin = new Thickness(0, 0, 4, 0) };
        privacyBtn.AddItem("Public", (int) CorporationPrivacy.Public);
        privacyBtn.AddItem("Unlisted", (int) CorporationPrivacy.Unlisted);
        privacyBtn.AddItem("Private", (int) CorporationPrivacy.Private);
        privacyBtn.SelectId((int) corp.Privacy);
        privacyBtn.OnItemSelected += args => privacyBtn.SelectId(args.Id);
        var setPrivacyBtn = new Button { Text = "Set" };
        setPrivacyBtn.OnPressed += _ => OnSetPrivacy?.Invoke(corpId, (CorporationPrivacy) privacyBtn.SelectedId);
        privacyRow.AddChild(privacyBtn);
        privacyRow.AddChild(setPrivacyBtn);
        _detailPanel.AddChild(privacyRow);

        // Members
        AddSectionHeader($"Members ({corp.Members.Count})");
        foreach (var member in corp.Members.OrderByDescending(m => m.Rank))
        {
            var capturedMember = member;
            var memberRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };

            var rankDropdown = new OptionButton { Margin = new Thickness(0, 0, 4, 0) };
            foreach (CorporationRank r in Enum.GetValues<CorporationRank>())
                rankDropdown.AddItem(r.ToString(), (int) r);
            rankDropdown.SelectId((int) member.Rank);
            rankDropdown.OnItemSelected += args =>
            {
                rankDropdown.SelectId(args.Id);
                OnSetMemberRank?.Invoke(corpId, capturedMember.UserId, (CorporationRank) args.Id);
            };

            var nameLabel = new Label
            {
                Text = $"{member.DisplayName}",
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
            };

            var kickBtn = new ConfirmButton
            {
                Text = "Kick",
                ConfirmationText = "Confirm",
                Margin = new Thickness(4, 0, 0, 0),
            };
            kickBtn.OnPressed += _ => OnKickMember?.Invoke(corpId, capturedMember.UserId);

            memberRow.AddChild(rankDropdown);
            memberRow.AddChild(nameLabel);
            memberRow.AddChild(kickBtn);
            _detailPanel.AddChild(memberRow);
        }

        var addPlayerBtn = new Button { Text = "+ Add Player", Margin = new Thickness(0, 4, 0, 0) };
        addPlayerBtn.OnPressed += _ =>
        {
            var dialog = new CorpAddMemberDialog();
            dialog.OnConfirm += userId => OnAddMember?.Invoke(corpId, userId);
            dialog.OpenCentered();
        };
        _detailPanel.AddChild(addPlayerBtn);

        // Station
        AddSectionHeader("Station");
        if (corp.Station != null)
        {
            var s = corp.Station;
            _detailPanel.AddChild(new Label { Text = $"Name: {s.StationName}" });
            _detailPanel.AddChild(new Label { Text = $"Save: {s.SavePath}" });
            _detailPanel.AddChild(new Label { Text = $"Active this round: {s.ActiveThisRound}", Margin = new Thickness(0, 0, 0, 4) });

            var stationBtns = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
            if (s.ActiveThisRound)
            {
                var saveBtn = new Button { Text = "Save Now", Margin = new Thickness(0, 0, 4, 0) };
                saveBtn.OnPressed += _ => OnSaveStation?.Invoke(corpId);
                stationBtns.AddChild(saveBtn);
            }

            var evictBtn = new ConfirmButton { Text = "Evict Station", ConfirmationText = "Confirm Evict" };
            evictBtn.OnPressed += _ => OnEvictStation?.Invoke(corpId);
            stationBtns.AddChild(evictBtn);
            _detailPanel.AddChild(stationBtns);
        }
        else
        {
            _detailPanel.AddChild(new Label { Text = "No station.", Margin = new Thickness(0, 0, 0, 4) });

            var grantRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
            var stationNameEdit = new LineEdit
            {
                PlaceHolder = "Station name...",
                HorizontalExpand = true,
                Margin = new Thickness(0, 0, 4, 0),
            };
            var grantBtn = new Button { Text = "Grant Station" };
            grantBtn.OnPressed += _ =>
            {
                var name = stationNameEdit.Text.Trim();
                if (!string.IsNullOrEmpty(name))
                    OnGrantStation?.Invoke(corpId, name);
            };
            grantRow.AddChild(stationNameEdit);
            grantRow.AddChild(grantBtn);
            _detailPanel.AddChild(grantRow);
        }

        // Archived stations
        if (corp.ArchivedStationFiles.Count > 0)
        {
            AddSectionHeader($"Archived Stations ({corp.ArchivedStationFiles.Count})");
            foreach (var archiveFile in corp.ArchivedStationFiles)
            {
                var capturedFile = archiveFile;
                var archiveRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };

                archiveRow.AddChild(new Label
                {
                    Text = capturedFile,
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0),
                });

                var nameEdit = new LineEdit
                {
                    PlaceHolder = "Restore as name...",
                    MinWidth = 140,
                    Margin = new Thickness(0, 0, 4, 0),
                };

                if (corp.Station == null)
                {
                    // Only show restore button if corp has no active station
                    var recoverBtn = new ConfirmButton { Text = "Restore", ConfirmationText = "Confirm" };
                    recoverBtn.OnPressed += _ =>
                    {
                        var name = nameEdit.Text.Trim();
                        if (string.IsNullOrEmpty(name))
                            name = corp.Name;
                        OnRecoverStation?.Invoke(corpId, capturedFile, name);
                    };
                    archiveRow.AddChild(nameEdit);
                    archiveRow.AddChild(recoverBtn);
                }

                _detailPanel.AddChild(archiveRow);
            }
        }

        // Danger zone
        AddSectionHeader("Danger Zone");
        var deleteBtn = new ConfirmButton
        {
            Text = "Delete Corporation",
            ConfirmationText = "Confirm Delete",
        };
        deleteBtn.OnPressed += _ => OnDeleteCorporation?.Invoke(corpId);
        _detailPanel.AddChild(deleteBtn);
    }

    // Helpers

    private void AddSectionHeader(string text)
    {
        _detailPanel.AddChild(new PanelContainer
        {
            Children =
            {
                new Label
                {
                    Text = text,
                    FontColorOverride = Color.FromHex("#AAAAAA"),
                    StyleClasses = { "LabelSmall" },
                },
            },
            Margin = new Thickness(0, 8, 0, 2),
        });
    }
}
