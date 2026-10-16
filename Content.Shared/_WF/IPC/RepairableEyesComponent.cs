using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._WF.IPC;

[RegisterComponent, NetworkedComponent]
public sealed partial class RepairableEyesComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField(required: true)]
    public TimeSpan Delay;

    [DataField(required: true)]
    public float SelfRepairMultiplier;
}
