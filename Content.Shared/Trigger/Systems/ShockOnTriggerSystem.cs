using Content.Shared.Electrocution;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Trigger.Systems;

public sealed class ShockOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShockOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<ShockOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextTrigger)
            return;

        ent.Comp.NextTrigger = now + ent.Comp.Cooldown;

        EntityUid? target;
        if (ent.Comp.TargetContainer)
        {
            // shock whoever is wearing this clothing item
            if (!_container.TryGetContainingContainer(ent.Owner, out var container))
                return;
            target = container.Owner;
        }
        else
        {
            target = ent.Comp.TargetUser ? args.User : ent.Owner;
        }

        if (target == null)
            return;

        _electrocution.TryDoElectrocution(target.Value, null, ent.Comp.Damage, ent.Comp.Duration, true, ignoreInsulation: true);
        args.Handled = true;
    }

}
