using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CS.BlipCartridge;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype, Serializable]
public sealed partial class RadarBlipPresetPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// The name to display in the UI.
    /// </summary>
    [DataField]
    public string Name = "Cool Cute preset 2000";

    /// <summary>
    /// The color set prototype ID to use for this blip preset.
    /// </summary>
    [DataField]
    public ProtoId<BlipColorSetPrototype> ColorSet = "BlipColorGreen";

    /// <summary>
    /// The shape set prototype ID to use for this blip preset.
    /// </summary>
    [DataField]
    public ProtoId<BlipShapeSetPrototype> ShapeSet = "BlipShapeCircle";

    /// <summary>
    /// The scale of the blip.
    /// </summary>
    [DataField]
    public float Scale = 1f;
}
