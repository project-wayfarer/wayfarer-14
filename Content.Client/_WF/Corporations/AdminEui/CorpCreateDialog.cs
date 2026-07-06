using System.Numerics;
using Content.Shared._WF.Corporations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._WF.Corporations.AdminEui;

public sealed class CorpCreateDialog : DefaultWindow
{
    public event Action<string, string, CorporationPrivacy>? OnConfirm;

    public CorpCreateDialog()
    {
        Title = "Create Corporation";
        MinSize = new Vector2(320, 200);

        var layout = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        var nameEdit = new LineEdit { PlaceHolder = "Corporation name...", Margin = new Thickness(0, 0, 0, 6) };

        var descEdit = new LineEdit { PlaceHolder = "Description...", Margin = new Thickness(0, 0, 0, 6) };

        var privacyBtn = new OptionButton { Margin = new Thickness(0, 0, 0, 8) };
        privacyBtn.AddItem("Public", (int) CorporationPrivacy.Public);
        privacyBtn.AddItem("Unlisted", (int) CorporationPrivacy.Unlisted);
        privacyBtn.AddItem("Private", (int) CorporationPrivacy.Private);
        privacyBtn.OnItemSelected += args => privacyBtn.SelectId(args.Id);

        var confirmBtn = new Button { Text = "Create" };
        confirmBtn.OnPressed += _ =>
        {
            var name = nameEdit.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            OnConfirm?.Invoke(name, descEdit.Text.Trim(), (CorporationPrivacy) privacyBtn.SelectedId);
            Close();
        };

        layout.AddChild(new Label { Text = "Name" });
        layout.AddChild(nameEdit);
        layout.AddChild(new Label { Text = "Description" });
        layout.AddChild(descEdit);
        layout.AddChild(new Label { Text = "Privacy" });
        layout.AddChild(privacyBtn);
        layout.AddChild(confirmBtn);

        Contents.AddChild(layout);
    }
}
