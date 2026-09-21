using Content.Client.UserInterface.Fragments;
using Content.Shared._WF.CartridgeLoader.Cartridges;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._WF.CartridgeLoader.Cartridges;

public sealed partial class OutlawObjectivesUi : UIFragment
{
    private OutlawObjectivesUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        if (_fragment is not { Disposed: false })
            _fragment = new OutlawObjectivesUiFragment();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not OutlawObjectivesUiState objectivesState)
            return;

        _fragment?.UpdateState(objectivesState.Entries);
    }
}
