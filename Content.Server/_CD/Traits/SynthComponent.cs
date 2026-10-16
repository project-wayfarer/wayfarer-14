namespace Content.Server._CD.Traits;

/// <summary>
/// Set players' blood to coolant
/// </summary>
[RegisterComponent, Access(typeof(SynthSystem))]
public sealed partial class SynthComponent : Component
{
    /// <summary>
    /// The chance that the synth is alerted of an ion storm
    /// </summary>
    /// Wayfarer: Not implemented, as Ion Storms are not a thing. they get Binary instead.
    [DataField]
    public float AlertChance = 0.3f;
}