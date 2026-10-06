namespace Content.Server._WF.Shuttles.Components;

[RegisterComponent]
public sealed partial class ShipValueComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public long Shown;

    [ViewVariables(VVAccess.ReadOnly)]
    public int Dc;
}
