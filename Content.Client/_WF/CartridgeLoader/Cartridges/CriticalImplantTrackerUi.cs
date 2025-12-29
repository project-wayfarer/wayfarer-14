using Content.Client.UserInterface.Fragments;
using Content.Shared._WF.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._WF.CartridgeLoader.Cartridges;

public sealed partial class CriticalImplantTrackerUi : UIFragment
{
    private CriticalImplantTrackerUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new CriticalImplantTrackerUiFragment();

        _fragment.OnRefreshPressed += () =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new CriticalImplantTrackerRefreshMessage()));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not CriticalImplantTrackerUiState trackerState)
            return;

        _fragment?.UpdateState(trackerState.Patients);
    }
}
