using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Server.Temperature.Components;
using Content.Shared._WF.IPC;
using Content.Shared.Alert;
using Content.Shared.Atmos.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._WF.IPC;

public sealed class PowerCellBodySystem : SharedPowerCellBodySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerCellBodyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PowerCellBodyComponent, PowerCellBodyDrinkDoAfterEvent>(OnDrinkDoAfter);
        SubscribeLocalEvent<ApcComponent, GetVerbsEvent<AlternativeVerb>>(OnApcVerbs);
    }

    private void OnMapInit(Entity<PowerCellBodyComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ChargeLevel = ent.Comp.ChargeLevels;
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PowerCellBodyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate += comp.UpdateInterval;
            var interval = (float) comp.UpdateInterval.TotalSeconds;

            if (!_powerCell.TryGetBatteryFromSlot(uid, out var cell, out var battery))
            {
                SetLevel((uid, comp), 0, false);
                continue;
            }

            if (ShouldDrain((uid, comp)))
                _battery.UseCharge(cell.Value, GetDrainRate((uid, comp), battery.MaxCharge, interval) * interval, battery);

            var level = (int) MathF.Round(battery.CurrentCharge / battery.MaxCharge * comp.ChargeLevels);
            SetLevel((uid, comp), level, true);
        }
    }

    private bool ShouldDrain(Entity<PowerCellBodyComponent> ent)
    {
        if (!ent.Comp.DrainWhenDead && _mobState.IsDead(ent))
            return false;

        return ent.Comp.DrainWithoutMind || !TryComp<MindContainerComponent>(ent, out var mind) || mind.HasMind;
    }

    private void SetLevel(Entity<PowerCellBodyComponent> ent, int level, bool hasCell)
    {
        if (ent.Comp.ChargeLevel == level && ent.Comp.HasCell == hasCell)
            return;

        ent.Comp.ChargeLevel = level;
        ent.Comp.HasCell = hasCell;
        Dirty(ent);

        _movement.RefreshMovementSpeedModifiers(ent);

        if (hasCell)
            _alerts.ShowAlert(ent, ent.Comp.BatteryAlert, (short) level);
        else
            _alerts.ShowAlert(ent, ent.Comp.NoBatteryAlert);
    }

    private float GetDrainRate(Entity<PowerCellBodyComponent> ent, float maxCharge, float frameTime)
    {
        var comp = ent.Comp;

        if (!TryComp<TemperatureComponent>(ent, out var temperature)
            || !TryComp<ThermalRegulatorComponent>(ent, out var regulator))
            return comp.DrainRate;

        var current = temperature.CurrentTemperature;
        var normal = regulator.NormalBodyTemperature;
        var comfortLimit = normal + regulator.ThermalRegulationTemperatureThreshold * comp.ComfortBandFraction;

        if (current > comfortLimit)
        {
            TryOverheat(ent, temperature, normal + regulator.ThermalRegulationTemperatureThreshold, frameTime);

            var extra = MathF.Min(current / comfortLimit, comp.MaxHeatMultiplier) - 1;
            return comp.DrainRate + MathF.Min(extra, maxCharge / comp.HeatDrainCapDivisor);
        }

        if (current < normal)
            return comp.DrainRate - comp.ColdDrainReduction * (1 - current / normal);

        return comp.DrainRate;
    }

    private void TryOverheat(Entity<PowerCellBodyComponent> ent, TemperatureComponent temperature, float upperLimit, float frameTime)
    {
        ent.Comp.OverheatAccumulator += frameTime;
        if (ent.Comp.OverheatAccumulator < (float) ent.Comp.OverheatInterval.TotalSeconds)
            return;

        ent.Comp.OverheatAccumulator -= (float) ent.Comp.OverheatInterval.TotalSeconds;

        if (!TryComp<FlammableComponent>(ent, out var flammable)
            || flammable.OnFire
            || temperature.CurrentTemperature <= temperature.HeatDamageThreshold)
            return;

        _popup.PopupEntity(Loc.GetString("power-cell-body-overheating"), ent, ent, PopupType.MediumCaution);

        var chance = Math.Clamp(temperature.CurrentTemperature / (upperLimit * ent.Comp.OverheatChanceDivisor), 0.001f, 0.9f);
        if (!_random.Prob(chance))
            return;

        _flammable.AdjustFireStacks(ent, ent.Comp.OverheatFireStacks, flammable);
        _flammable.Ignite(ent, ent, flammable);
    }

    private void OnApcVerbs(Entity<ApcComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<PowerCellBodyComponent>(args.User, out var body)
            || !body.CanDrinkFromApc
            || !HasComp<BatteryComponent>(ent)
            || !_powerCell.TryGetBatteryFromSlot(args.User, out _))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartDrink(user, ent, body),
            Text = Loc.GetString("power-cell-body-drink-verb"),
            Icon = body.DrinkVerbIcon,
        });
    }

    private void StartDrink(EntityUid user, EntityUid source, PowerCellBodyComponent body)
    {
        var args = new DoAfterArgs(EntityManager, user, body.DrinkDelay, new PowerCellBodyDrinkDoAfterEvent(), user, source)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = body.DrinkRange,
            RequireCanInteract = true,
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnDrinkDoAfter(Entity<PowerCellBodyComponent> ent, ref PowerCellBodyDrinkDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } source)
            return;

        if (!TryComp<BatteryComponent>(source, out var sourceBattery)
            || !_powerCell.TryGetBatteryFromSlot(ent, out var cell, out var cellBattery))
            return;

        args.Handled = true;

        var amount = MathF.Min(ent.Comp.DrinkAmount, sourceBattery.CurrentCharge);
        amount = MathF.Min(amount, cellBattery.MaxCharge - cellBattery.CurrentCharge);

        if (amount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("power-cell-body-drink-empty", ("target", source)), ent, ent);
            return;
        }

        _battery.UseCharge(source, amount, sourceBattery);
        _battery.SetCharge(cell.Value, cellBattery.CurrentCharge + amount, cellBattery);

        _popup.PopupEntity(Loc.GetString("power-cell-body-drink-success"), ent, ent, PopupType.SmallCaution);
        _audio.PlayPvs(ent.Comp.DrinkSound, source);

        if (ent.Comp.DrinkEffect is { } effect)
            Spawn(effect, Transform(source).Coordinates);
    }
}
