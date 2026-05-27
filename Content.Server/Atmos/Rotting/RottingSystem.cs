using Content.Server.Atmos.EntitySystems;
using Content.Server.Temperature.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Events;
using Content.Shared.Damage;
using Robust.Server.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.Atmos.Rotting;

public sealed class RottingSystem : SharedRottingSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RottingComponent, BeingGibbedEvent>(OnGibbed);

        SubscribeLocalEvent<TemperatureComponent, IsRottingEvent>(OnTempIsRotting);
    }

    private void OnGibbed(EntityUid uid, RottingComponent component, BeingGibbedEvent args)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return;

        if (!TryComp<PerishableComponent>(uid, out var perishable))
            return;

        var molsToDump = perishable.MolsPerSecondPerUnitMass * physics.FixturesMass * (float)component.TotalRotTime.TotalSeconds;
        var tileMix = _atmosphere.GetTileMixture(uid, excite: true);
        tileMix?.AdjustMoles(Gas.Ammonia, molsToDump);
    }

    private void OnTempIsRotting(EntityUid uid, TemperatureComponent component, ref IsRottingEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = component.CurrentTemperature < Atmospherics.T0C + 0.85f;
    }

    /// <summary>
    /// Is anything speeding up or slowing down the decay?
    /// e.g. buried in a grave (speeds up), or in cryostorage (slows down)
    /// TODO: hot temperatures increase rot?
    /// </summary>
    /// <returns></returns>
    private float GetRotRate(EntityUid uid)
    {
        if (_container.TryGetContainingContainer((uid, null, null), out var container))
        {
            if (TryComp<ProRottingContainerComponent>(container.Owner, out var rotContainer))
                return rotContainer.DecayModifier;

            if (TryComp<SlowDecayContainerComponent>(container.Owner, out var slowContainer))
                return slowContainer.DecayModifier;
        }

        return 1f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var perishQuery = EntityQueryEnumerator<PerishableComponent>();
        while (perishQuery.MoveNext(out var uid, out var perishable))
        {
            if (_timing.CurTime < perishable.RotNextUpdate)
                continue;
            perishable.RotNextUpdate += perishable.PerishUpdateRate;

            var stage = PerishStage((uid, perishable), MaxStages);
            if (stage != perishable.Stage)
            {
                perishable.Stage = stage;
                Dirty(uid, perishable);
            }

            if (IsRotten(uid) || !IsRotProgressing(uid, perishable))
                continue;

            perishable.RotAccumulator += perishable.PerishUpdateRate * GetRotRate(uid);
            if (perishable.RotAccumulator >= perishable.RotAfter)
            {
                var rot = AddComp<RottingComponent>(uid);
                rot.NextRotUpdate = _timing.CurTime + rot.RotUpdateRate;
            }
        }

        var rotQuery = EntityQueryEnumerator<RottingComponent, PerishableComponent, TransformComponent>();
        while (rotQuery.MoveNext(out var uid, out var rotting, out var perishable, out var xform))
        {
            if (_timing.CurTime < rotting.NextRotUpdate) // This is where it starts to get noticable on larger animals, no need to run every second
                continue;
            rotting.NextRotUpdate += rotting.RotUpdateRate;

            if (!IsRotProgressing(uid, perishable))
                continue;
            rotting.TotalRotTime += rotting.RotUpdateRate * GetRotRate(uid);

            if (rotting.DealDamage && TryComp<DamageableComponent>(uid, out var damageable)) // Wayfarer: add && TryComp<DamageableComponent>(uid, out var damageable)
            {
                var damage = rotting.Damage * rotting.RotUpdateRate.TotalSeconds;
                //_damageable.TryChangeDamage(uid, damage, true, false); // Wayfarer: Comment this in favor of the checks below:
                // Wayfarer: Rot gibbing damage cap.
                // Check if we've hit the blunt damage cap
                if (rotting.TotalBluntDamageDealt >= rotting.DamageCap)
                {
                    // Remove blunt damage from the damage to be dealt
                    damage.DamageDict.Remove("Blunt");
                }
                else
                {
                    // Calculate how much blunt damage we're about to deal
                    var bluntDamage = (float)damage.DamageDict.GetValueOrDefault("Blunt", 0);

                    // If this would exceed the cap, reduce it
                    if (rotting.TotalBluntDamageDealt + bluntDamage > rotting.DamageCap)
                    {
                        var remainingDamage = rotting.DamageCap - rotting.TotalBluntDamageDealt;
                        damage.DamageDict["Blunt"] = remainingDamage;
                        rotting.TotalBluntDamageDealt = rotting.DamageCap;
                    }
                    else
                    {
                        rotting.TotalBluntDamageDealt += bluntDamage;
                    }
                }

                if (damage.DamageDict.Count > 0)
                    _damageable.TryChangeDamage(uid, damage, true, false);
                // End Wayfarer
            }

            if (TryComp<RotIntoComponent>(uid, out var rotInto))
            {
                var stage = RotStage(uid, rotting, perishable);
                if (stage >= rotInto.Stage)
                {
                    Spawn(rotInto.Entity, xform.Coordinates);
                    QueueDel(uid);
                    continue;
                }
            }

            if (!TryComp<PhysicsComponent>(uid, out var physics))
                continue;
            // We need a way to get the mass of the mob alone without armor etc in the future
            // or just remove the mass mechanics altogether because they aren't good.

            // Wayfarer: Wrapped the gas emmission to only emit ammonia if the blunt damage cap hasn't been hit yet
            if (!rotting.DealDamage || rotting.TotalBluntDamageDealt < rotting.DamageCap)
            {
                var molRate = perishable.MolsPerSecondPerUnitMass * (float)rotting.RotUpdateRate.TotalSeconds;
                var tileMix = _atmosphere.GetTileMixture(uid, excite: true);
                tileMix?.AdjustMoles(Gas.Ammonia, molRate * physics.FixturesMass);
            }
            // End Wayfarer
        }
    }
}
