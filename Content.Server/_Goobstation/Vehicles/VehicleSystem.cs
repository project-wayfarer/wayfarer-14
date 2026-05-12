using Content.Shared._Goobstation.Vehicles; // Frontier: migrate under _Goobstation
using Content.Server._NF.Radar; // Frontier
using Content.Shared.Buckle.Components; // Frontier
using Content.Shared._NF.Radar; // Frontier
using Content.Shared._NF.Vehicle.Components; // Wayfarer
using Content.Shared.Buckle; // Wayfarer
using Content.Shared.Damage; // Wayfarer
using Content.Shared.Stunnable; // Wayfarer
using Robust.Shared.Random; // Wayfarer

namespace Content.Server._Goobstation.Vehicles; // Frontier: migrate under _Goobstation

public sealed class VehicleSystem : SharedVehicleSystem
{
    //// Frontier: extra logic (radar blips, faction stuff)
    [Dependency] private readonly RadarBlipSystem _radar = default!;
    // Wayfarer: rider knockoff on damage
    [Dependency] private readonly SharedBuckleSystem _buckleSystem = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    // End Wayfarer

    /// <summary>
    /// Configures the radar blip for a vehicle entity.
    /// </summary>
    // Wayfarer: override Initialize to subscribe to rider damage event
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleRiderComponent, DamageChangedEvent>(OnRiderDamageChanged);
    }
    // End Wayfarer

    protected override void OnStrapped(Entity<VehicleComponent> ent, ref StrappedEvent args)
    {
        base.OnStrapped(ent, ref args);
        _radar.SetupVehicleRadarBlip(ent);
    }

    protected override void OnUnstrapped(Entity<VehicleComponent> ent, ref UnstrappedEvent args)
    {
        RemComp<RadarBlipComponent>(ent);
        base.OnUnstrapped(ent, ref args);
    }

    protected override void HandleEmag(Entity<VehicleComponent> ent)
    {
        RemComp<RadarBlipComponent>(ent);
    }

    protected override void HandleUnemag(Entity<VehicleComponent> ent)
    {
        if (ent.Comp.Driver != null)
            _radar.SetupVehicleRadarBlip(ent);
    }
    // End Frontier

    // Wayfarer: knock rider off vehicle on damage with 70% chance
    private void OnRiderDamageChanged(Entity<VehicleRiderComponent> ent, ref DamageChangedEvent args)
    {
        // Only trigger on actual damage, not healing
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

        if (!_random.Prob(0.70f))
            return;

        if (!TryComp<BuckleComponent>(ent, out var buckle) || !buckle.Buckled || buckle.BuckledTo == null)
            return;

        _buckleSystem.TryUnbuckle(ent, ent, buckleComp: buckle);
        _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(1), refresh: true, force: true);
    }
    // End Wayfarer
}
