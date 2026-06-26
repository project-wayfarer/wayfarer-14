using Content.Shared._WF.Weather; // Wayfarer
using Content.Shared.Damage; // Wayfarer
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

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

    // Wayfarer: Damage dealt each DamageInterval to mobs on a tile the weather reaches.
    [ViewVariables(VVAccess.ReadWrite), DataField("damage")]
    public DamageSpecifier? Damage;

    [ViewVariables(VVAccess.ReadWrite), DataField("damageInterval")]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(1);

    // Wayfarer: Marks the weather as particulate or permeating. See WeatherShelter.
    [ViewVariables(VVAccess.ReadWrite), DataField("particulate")]
    public WeatherParticulate? Particulate;

    [ViewVariables(VVAccess.ReadWrite), DataField("permeating")]
    public WeatherPermeating? Permeating;
    // End Wayfarer
}
