using System.Numerics;
using Content.Client.Administration.UI.CustomControls;
using Content.Shared.Administration;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._WF.Corporations.AdminEui;

public sealed class CorpAddMemberDialog : DefaultWindow
{
    /// <summary>Fired when the admin confirms — passes the selected player's session ID.</summary>
    public event Action<Guid>? OnConfirm;

    public CorpAddMemberDialog()
    {
        Title = "Add Player to Corporation";
        MinSize = new Vector2(400, 500);

        var layout = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        var playerList = new PlayerListControl
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 8),
        };

        PlayerInfo? selected = null;
        playerList.OnSelectionChanged += p => selected = p;

        var confirmBtn = new Button { Text = "Add as Member", Disabled = true };
        playerList.OnSelectionChanged += p => confirmBtn.Disabled = p == null;

        confirmBtn.OnPressed += _ =>
        {
            if (selected == null) return;
            OnConfirm?.Invoke(selected.SessionId.UserId);
            Close();
        };

        layout.AddChild(playerList);
        layout.AddChild(confirmBtn);
        Contents.AddChild(layout);
    }
}
