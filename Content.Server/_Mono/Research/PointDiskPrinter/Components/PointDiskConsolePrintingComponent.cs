// Wayfarer: Ported from Monolith PR #1408
namespace Content.Server._Mono.Research.PointDiskPrinter.Components;

[RegisterComponent]
public sealed partial class PointDiskConsolePrintingComponent : Component
{
    public TimeSpan FinishTime;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Disk1K = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Disk5K = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Disk10K = false;

    // Wayfarer
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Disk50K = false;
    //End Wayfarer
}
