using Content.Shared._CS.BlipCartridge;
using Robust.Shared.Prototypes;

namespace Content.Shared._CS.BlipCartridge;

/// <summary>
/// This component is used to add a radar blip for your PDA when the Blip Cartridge is equipped!
/// Great for dying in the middle of nowhere and having pirates ransom your body!
/// </summary>
[RegisterComponent]
public sealed partial class BlipCartridgeComponent : Component
{
    /// <summary>
    /// Default preset for the blip cartridge.
    /// </summary>
    [DataField]
    public ProtoId<RadarBlipPresetPrototype> DefaultPreset { get; set; } = "BlipPresetCivilian";

    /// <summary>
    /// Current preset for the blip cartridge.
    /// </summary>
    [DataField]
    public ProtoId<RadarBlipPresetPrototype> CurrentPreset { get; set; } = "BlipPresetCivilian";

    // stored blip data for like when the cartridge is removed and for to be re-added later
    /// <summary>
    /// Color Table Set for the blip.
    /// </summary>
    [DataField]
    public ProtoId<BlipColorSetPrototype> BlipColor { get; set; } = "BlipColorRed";

    /// <summary>
    /// The Highlighted Color Table Set for the blip.
    /// </summary>
    [DataField]
    public ProtoId<BlipColorSetPrototype> BlipHighlightedColor { get; set; } = "BlipColorRed";

    /// <summary>
    /// Shape Table Set for the blip.
    /// </summary>
    [DataField]
    public ProtoId<BlipShapeSetPrototype> BlipShape { get; set; } = "BlipShapeCircle";

    /// <summary>
    /// Scale of the blip.
    /// </summary>
    [DataField]
    public float Scale { get; set; } = 3f;

    /// <summary>
    /// Whether this blip is enabled and should be shown on radar.
    /// </summary>
    [DataField]
    public bool Enabled { get; set; } = true;

    // Settings that can setting for it
    /// <summary>
    /// A list that maps color names to their corresponding color values.
    /// prototypes
    /// </summary>
    [DataField]
    public List<ProtoId<BlipColorSetPrototype>> ColorTable = new()
    {
        "BlipColorRed",
        "BlipColorOrange",
        "BlipColorGold",
        "BlipColorYellow",
        "BlipColorGreen",
        "BlipColorBlue",
        "BlipColorCyan",
        "BlipColorTeal",
    };

    /// <summary>
    /// A list that maps shape names to their corresponding shape values.
    /// proots
    /// </summary>
    [DataField]
    public List<ProtoId<BlipShapeSetPrototype>> ShapeTable = new()
    {
        "BlipShapeCircle",
        "BlipShapeSquare",
        "BlipShapeTriangle",
        "BlipShapeDiamond",
        "BlipShapeHexagon",
        "BlipShapeStar",
        "BlipShapeArrow",
        // "BlipShapeHeart", // doesnt work
        "BlipShapeX",
    };

    /// <summary>
    /// Available blip presets for the cartridge.
    /// </summary>
    [DataField]
    public List<ProtoId<RadarBlipPresetPrototype>> Presets = new()
    {
        "BlipPresetCivilian",
        "BlipPresetMercenary",
        "BlipPresetCommand",
        "BlipPresetPirate",
        "BlipPresetMedical",
        "BlipPresetEngineering",
        "BlipPresetSecurity",
        "BlipPresetScience",
        "BlipPresetSupply",
        "BlipPresetHorny",
        "BlipPresetBooty",
        "BlipPresetMailCourier",
    };

    public bool IsFlashed { get; set; } = false;
}

/// <summary>
///     Component attached to the PDA a BlipCartridge cartridge is inserted into for interaction handling
/// </summary>
[RegisterComponent]
public sealed partial class BlipCartridgeInteractionComponent : Component;
