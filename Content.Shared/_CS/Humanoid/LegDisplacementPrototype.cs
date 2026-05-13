using Content.Shared.DisplacementMap;
using Robust.Shared.Prototypes;

namespace Content.Shared._CS.Humanoid;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype] // Wayfarer: explicit prototype name is not needed
public sealed partial class LegDisplacementPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public Dictionary<string, DisplacementData> Displacements = new();

    [DataField]
    public Dictionary<string, DisplacementData> FemaleDisplacements = new();

    [DataField]
    public Dictionary<string, DisplacementData> MaleDisplacements = new();
}
