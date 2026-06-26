using Content.Shared.FixedPoint;

namespace Content.Shared.Medical.Healing;

public sealed partial class HealingComponent
{
    /// <summary>
    /// Optional per-group damage thresholds, if the target's total damage in a group equals or exceeds this value, that means you'll need alternative means to heal them.
    /// Keys are damage group prototype ids ("Brute", "Burn", etc) or individual damage type ids. The system only currently allows for one or the other, otherwise there may be issues.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, FixedPoint2>? MaxHealableDamage;
}
