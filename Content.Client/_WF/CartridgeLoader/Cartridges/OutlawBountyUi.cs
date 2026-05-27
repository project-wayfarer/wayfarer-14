using Content.Client.Message;
using Content.Client.UserInterface.Fragments;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Pirate;
using Content.Shared._NF.Pirate.Prototypes;
using Content.Shared._WF.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._WF.CartridgeLoader.Cartridges;

public sealed partial class OutlawBountyUi : UIFragment
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private Control? _root;
    private BoxContainer? _bountyList;

    public override Control GetUIFragmentRoot()
    {
        return _root!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        IoCManager.InjectDependencies(this);

        _bountyList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(8, 8, 8, 8),
        };

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        scroll.AddChild(_bountyList);

        var panel = new PanelContainer
        {
            StyleClasses = { "BackgroundDark" },
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(1, 0, 2, 0),
        };
        panel.AddChild(scroll);

        _root = panel;
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not OutlawBountyUiState bountyState || _bountyList is null)
            return;

        _bountyList.RemoveAllChildren();

        foreach (var data in bountyState.Bounties)
        {
            if (!_proto.TryIndex<PirateBountyPrototype>(data.Bounty, out var proto))
                continue;
            _bountyList.AddChild(BuildEntry(data, proto));
        }
    }

    private PanelContainer BuildEntry(PirateBountyData data, PirateBountyPrototype proto)
    {
        var panel = new PanelContainer
        {
            StyleClasses = { "AngleRect" },
            Margin = new Thickness(0, 0, 0, 3),
            HorizontalExpand = true,
        };

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(4, 2, 4, 2),
        };

        var manifestItems = new List<string>();
        foreach (var entry in proto.Entries)
        {
            manifestItems.Add(Loc.GetString("pirate-bounty-console-manifest-entry",
                ("amount", entry.Amount),
                ("item", Loc.GetString(entry.Name))));
        }

        var manifestLabel = new RichTextLabel { HorizontalExpand = true };
        manifestLabel.SetMarkup(Loc.GetString("pirate-bounty-console-manifest-label",
            ("item", string.Join(", ", manifestItems))));
        content.AddChild(manifestLabel);

        var rewardLabel = new RichTextLabel();
        rewardLabel.SetMarkup(Loc.GetString("pirate-bounty-console-reward-label",
            ("reward", BankSystemExtensions.ToIndependentString(proto.Reward))));
        content.AddChild(rewardLabel);

        var bottomRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        var descLabel = new RichTextLabel { HorizontalExpand = true };
        descLabel.SetMarkup(Loc.GetString("pirate-bounty-console-description-label",
            ("description", Loc.GetString(proto.Description))));
        bottomRow.AddChild(descLabel);

        if (data.Accepted)
        {
            var acceptedLabel = new RichTextLabel { Margin = new Thickness(0, 0, 6, 0) };
            acceptedLabel.SetMarkup(Loc.GetString("outlaw-bounty-accepted-marker"));
            bottomRow.AddChild(acceptedLabel);
        }

        var idLabel = new RichTextLabel { HorizontalAlignment = Control.HAlignment.Right };
        idLabel.SetMarkup(Loc.GetString("pirate-bounty-console-id-label", ("id", data.Id)));
        bottomRow.AddChild(idLabel);

        content.AddChild(bottomRow);
        panel.AddChild(content);
        return panel;
    }
}
