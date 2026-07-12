using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._NF.Shipyard.Components;

public sealed partial class ShipyardVoucherComponent
{
    /// <summary>
    ///  If set, the voucher can only redeem the ships in this list.
    /// </summary>
    [DataField]
    public List<ProtoId<VesselPrototype>>? ShipWhitelist = null;
}
