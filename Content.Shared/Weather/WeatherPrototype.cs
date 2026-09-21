using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared._WF.Weather; // Wayfarer
using Content.Shared.Damage; // Wayfarer

namespace Content.Shared.Weather;

[Prototype]
public sealed partial class WeatherPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [ViewVariables(VVAccess.ReadWrite), DataField("sprite", required: true)]
    public SpriteSpecifier Sprite = default!;

    [ViewVariables(VVAccess.ReadWrite), DataField("color")]
    public Color? Color;

    /// <summary>
    /// Sound to play on the affected areas.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("sound")]
    public SoundSpecifier? Sound;

    // Wayfarer
    // Dealt once a second to players the weather can reach.
    [ViewVariables(VVAccess.ReadWrite), DataField("damage")]
    public DamageSpecifier? Damage;

    // Not required, so a weather added upstream later still loads on a merge.
    [ViewVariables(VVAccess.ReadWrite), DataField("shelterType")]
    public WeatherShelter ShelterType = WeatherShelter.Particulate;
    // End Wayfarer
}
