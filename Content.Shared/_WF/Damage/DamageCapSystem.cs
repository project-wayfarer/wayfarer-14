using Content.Shared.Damage;

namespace Content.Shared._WF.Damage;

public sealed class DamageCapSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageCapComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    private void OnBeforeDamageChanged(Entity<DamageCapComponent> ent, ref BeforeDamageChangedEvent args)
    {
        var cap = ent.Comp.DamageCap;
        if (cap <= 0)
            return;

        if (!TryComp<DamageableComponent>(ent.Owner, out var damageable))
            return;

        var currentDamage = damageable.Damage.DamageDict;
        var delta = args.Damage;

        foreach (var (typeId, addAmount) in delta.DamageDict)
        {
            if (addAmount <= 0)
                continue; // probably needed in case of healing?

            var current = currentDamage.GetValueOrDefault(typeId);
            var roomLeft = cap - current;
            if (roomLeft <= 0)
            {
                // Already at or above cap
                delta.DamageDict.Remove(typeId);
            }
            else if (addAmount > roomLeft)
            {
                // Clamp it
                delta.DamageDict[typeId] = roomLeft;
            }
        }
    }
}
