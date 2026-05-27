// Wayfarer: Ported from Monolith PR #1408
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Mono.Research.PointDiskPrinter.Components;

[RegisterComponent]
public sealed partial class PointDiskConsoleComponent : Component
{
    /// <summary>
    /// How much it costs to print a 1k point disk
    /// </summary>
    [DataField("pricePer1KDisk"), ViewVariables(VVAccess.ReadWrite)]
    public int PricePer1KDisk = 1000;

    /// <summary>
    /// How much it costs to print a 5k point disk
    /// </summary>
    [DataField("pricePer5KDisk"), ViewVariables(VVAccess.ReadWrite)]
    public int PricePer5KDisk = 5000;

    /// <summary>
    /// How much it costs to print a 10k point disk
    /// </summary>
    [DataField("pricePer10KDisk"), ViewVariables(VVAccess.ReadWrite)]
    public int PricePer10KDisk = 10000;
       /// <summary>

    // Wayfarer
    /// How much it costs to print a 50k point disk
    /// </summary>
    [DataField("pricePer50KDisk"), ViewVariables(VVAccess.ReadWrite)]
    public int PricePer50KDisk = 50000;
    // End Wayfarer

    /// <summary>
    /// The prototype of what's being printed
    /// </summary>
    [DataField("diskPrototype1K", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string Disk1KPrototype = "ResearchDisk";

    [DataField("diskPrototype5K", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string Disk5KPrototype = "ResearchDisk5000";

    [DataField("diskPrototype10K", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string Disk10KPrototype = "ResearchDisk10000";

    // Wayfarer
    [DataField("diskPrototype50K", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string Disk50KPrototype = "ResearchDisk50000";
    // End Wayfarer

    /// <summary>
    /// How long it takes to print <see cref="PointDiskPrototype"/>
    /// </summary>
    [DataField("printDuration"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan PrintDuration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The sound made when printing occurs
    /// </summary>
    [DataField("printSound")]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
}
