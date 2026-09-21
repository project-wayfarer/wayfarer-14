namespace Content.Shared._DV.Traits;

public sealed partial class TraitPrototype
{
    /// <summary>
    /// Keeps the trait off the traits tab and doesn't count towards total traits.
    /// </summary>
    [DataField]
    public bool Hidden;
}
