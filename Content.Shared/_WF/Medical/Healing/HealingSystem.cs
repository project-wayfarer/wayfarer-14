using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Healing;

public sealed partial class HealingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>
    /// checks if the target's damage exceeds the item's MaxHealableDamage thresholds, aka, the big ouch
    /// </summary>
    private bool IsHealingThresholdExceeded(Entity<HealingComponent> healing, Entity<DamageableComponent> target)
    {
        if (healing.Comp.MaxHealableDamage is not { } thresholds)
            return false;

        foreach (var (key, max) in thresholds)
        {
            // First, try as a specific damage type, like heat, slash and etc.
            if (target.Comp.Damage.DamageDict.TryGetValue(key, out var typeDamage) && typeDamage >= max)
                return true;

            // If not a damage type, try as a damage group, like brute and burn... The generalized approach.
            if (_proto.TryIndex<DamageGroupPrototype>(key, out var groupProto) &&
                target.Comp.Damage.TryGetDamageInGroup(groupProto, out var groupTotal) &&
                groupTotal >= max)
                return true;
        }
        return false;
    }
}
