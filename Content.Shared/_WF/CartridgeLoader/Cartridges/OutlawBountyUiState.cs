using Content.Shared._NF.Pirate;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class OutlawBountyUiState : BoundUserInterfaceState
{
    public List<PirateBountyData> Bounties { get; }

    public OutlawBountyUiState(List<PirateBountyData> bounties)
    {
        Bounties = bounties;
    }
}
