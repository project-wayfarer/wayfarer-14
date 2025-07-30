using Content.Server.Explosion.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Cargo;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server.Atmos.EntitySystems
{
    [UsedImplicitly]
    public sealed class GasTankSystem : SharedGasTankSystem
    {
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly ExplosionSystem _explosions = default!;
        [Dependency] private readonly SharedAudioSystem _audioSys = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly ThrowingSystem _throwing = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

        private const float TimerDelay = 0.5f;
        private float _timer = 0f;
        private const float MinimumSoundValvePressure = 10.0f;
        private float _maxExplosionRange;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GasTankComponent, EntParentChangedMessage>(OnParentChange);
            SubscribeLocalEvent<GasTankComponent, GasAnalyzerScanEvent>(OnAnalyzed);
            SubscribeLocalEvent<GasTankComponent, PriceCalculationEvent>(OnGasTankPrice);
            SubscribeLocalEvent<GasTankComponent, GetVerbsEvent<Verb>>(AddToggleAlertsVerb);
            Subs.CVar(_cfg, CCVars.AtmosTankFragment, UpdateMaxRange, true);
        }

        private void UpdateMaxRange(float value)
        {
            _maxExplosionRange = value;
        }

        public override void UpdateUserInterface(Entity<GasTankComponent> ent)
        {
            var (owner, component) = ent;
            _ui.SetUiState(owner, SharedGasTankUiKey.Key,
                new GasTankBoundUserInterfaceState
                {
                    TankPressure = component.Air?.Pressure ?? 0,
                });
        }

        private void OnParentChange(EntityUid uid, GasTankComponent component, ref EntParentChangedMessage args)
        {
            // When an item is moved from hands -> pockets, the container removal briefly dumps the item on the floor.
            // So this is a shitty fix, where the parent check is just delayed. But this really needs to get fixed
            // properly at some point.
            component.CheckUser = true;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            _timer += frameTime;

            if (_timer < TimerDelay)
                return;

            _timer -= TimerDelay;

            var query = EntityQueryEnumerator<GasTankComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                var gasTank = (uid, comp);
                if (comp.IsValveOpen && !comp.IsLowPressure && comp.OutputPressure > 0)
                {
                    ReleaseGas(gasTank);
                }

                if (comp.CheckUser)
                {
                    comp.CheckUser = false;
                    if (Transform(uid).ParentUid != comp.User)
                    {
                        DisconnectFromInternals(gasTank);
                        continue;
                    }
                }

                if (comp.Air != null)
                {
                    _atmosphereSystem.React(comp.Air, comp);
                }

                CheckStatus(gasTank);
                PressureBeep(gasTank);

                if ((comp.IsConnected || comp.IsValveOpen) && _ui.IsUiOpen(uid, SharedGasTankUiKey.Key))
                {
                    UpdateUserInterface(gasTank);
                }
            }
        }

        private void ReleaseGas(Entity<GasTankComponent> gasTank)
        {
            var removed = RemoveAirVolume(gasTank, gasTank.Comp.ValveOutputRate * TimerDelay);
            var environment = _atmosphereSystem.GetContainingMixture(gasTank.Owner, false, true);
            if (environment != null)
            {
                _atmosphereSystem.Merge(environment, removed);
            }
            var strength = removed.TotalMoles * MathF.Sqrt(removed.Temperature);
            var dir = _random.NextAngle().ToWorldVec();
            _throwing.TryThrow(gasTank, dir * strength, strength);
            if (gasTank.Comp.OutputPressure >= MinimumSoundValvePressure)
                _audioSys.PlayPvs(gasTank.Comp.RuptureSound, gasTank);
        }

        public GasMixture? RemoveAir(Entity<GasTankComponent> gasTank, float amount)
        {
            var gas = gasTank.Comp.Air?.Remove(amount);
            CheckStatus(gasTank);
            return gas;
        }

        public GasMixture RemoveAirVolume(Entity<GasTankComponent> gasTank, float volume)
        {
            var component = gasTank.Comp;
            if (component.Air == null)
                return new GasMixture(volume);

            var molesNeeded = component.OutputPressure * volume / (Atmospherics.R * component.Air.Temperature);

            var air = RemoveAir(gasTank, molesNeeded);

            if (air != null)
                air.Volume = volume;
            else
                return new GasMixture(volume);

            return air;
        }

        public void AssumeAir(Entity<GasTankComponent> ent, GasMixture giver)
        {
            _atmosphereSystem.Merge(ent.Comp.Air, giver);
            CheckStatus(ent);
        }

        public void CheckStatus(Entity<GasTankComponent> ent)
        {
            var (owner, component) = ent;
            if (component.Air == null)
                return;

            var pressure = component.Air.Pressure;

            if (pressure > component.TankFragmentPressure && _maxExplosionRange > 0)
            {
                // Give the gas a chance to build up more pressure.
                for (var i = 0; i < 3; i++)
                {
                    _atmosphereSystem.React(component.Air, component);
                }

                pressure = component.Air.Pressure;
                var range = MathF.Sqrt((pressure - component.TankFragmentPressure) / component.TankFragmentScale);

                // Let's cap the explosion, yeah?
                // !1984
                range = Math.Min(Math.Min(range, GasTankComponent.MaxExplosionRange), _maxExplosionRange);

                _explosions.TriggerExplosive(owner, radius: range);

                return;
            }

            if (pressure > component.TankRupturePressure)
            {
                if (component.Integrity <= 0)
                {
                    var environment = _atmosphereSystem.GetContainingMixture(owner, false, true);
                    if (environment != null)
                        _atmosphereSystem.Merge(environment, component.Air);

                    _audioSys.PlayPvs(component.RuptureSound, Transform(owner).Coordinates, AudioParams.Default.WithVariation(0.125f));

                    QueueDel(owner);
                    return;
                }

                component.Integrity--;
                return;
            }

            if (pressure > component.TankLeakPressure)
            {
                if (component.Integrity <= 0)
                {
                    var environment = _atmosphereSystem.GetContainingMixture(owner, false, true);
                    if (environment == null)
                        return;

                    var leakedGas = component.Air.RemoveRatio(0.25f);
                    _atmosphereSystem.Merge(environment, leakedGas);
                }
                else
                {
                    component.Integrity--;
                }

                return;
            }

            if (component.Integrity < 3)
                component.Integrity++;
        }

        // COYOTE START: Added pressure beep warning system thing
        /// <summary>
        /// Play some kind of beep if the pressure is low enough.
        /// Runs off a system of thresholds, which are defined in the GasTankComponent.
        /// once tripped, they need to have the pressure go above the threshold to be reset.
        /// </summary>
        private void PressureBeep(Entity<GasTankComponent> gasTank)
        {
            var component = gasTank.Comp;
            if (component.HushAlerts)
                return; // no alerts if the tank is muted
            TryComp<ActiveJetpackComponent>(gasTank.Owner, out var jetpack);
            var amJetting = jetpack is not null;
            var amInternals = component.User is not null;
            if (!amJetting && !amInternals)
                return; // requires to be connected to internals and or be an active jetpack to beep
            var user = component.User ?? Transform(gasTank.Owner).ParentUid;
            var currPressure = component.Air.Pressure;
            const float maxPressure = Atmospherics.OneAtmosphere * 10; // close enough
            var pressureFraction = currPressure / maxPressure;
            // now go through the thresholds and see if we need to beep
            foreach (var threshold in component.AlertThresholds)
            {
                // first some lousekeeping, check if the pressure is above the threshold
                // and untrip the alert if it is
                if (pressureFraction > threshold.PressurePercentThreshold)
                {
                    threshold.Tripped = false;
                    continue;
                }
                if (threshold.Tripped)
                {
                    continue; // already tripped, no need to beep again
                }
                // if we got here, the pressure is below the threshold and the alert is not tripped
                threshold.Tripped = true; // trip the alert
                var audioParams = AudioParams.Default.WithVariation(0.125f).WithVolume(-2f);
                // play the alert sound, depending on if we are an internals or a jetpack
                // if we are both, play the internals sound
                _audioSys.PlayGlobal(
                    amInternals
                        ? threshold.AlertSound
                        : threshold.JetpackAlertSound,
                    user,
                    audioParams);
                break; // only play the first alert that is tripped
            }
        }

        private void AddToggleAlertsVerb(EntityUid uid, GasTankComponent component, GetVerbsEvent<Verb> args)
        {
            if (args.Hands == null
                || !args.CanAccess
                || !args.CanInteract)
                return;

            var onOff = component.HushAlerts
                ? Loc.GetString("gas-tank-toggle-alerts-off")
                : Loc.GetString("gas-tank-toggle-alerts-on");
            var onOffMsg = component.HushAlerts
                ? Loc.GetString("gas-tank-toggle-alerts-message-off")
                : Loc.GetString("gas-tank-toggle-alerts-message-on");
            var popupText = component.HushAlerts
                ? Loc.GetString("gas-tank-toggle-alerts-popup-off")
                : Loc.GetString("gas-tank-toggle-alerts-popup-on");
            Verb verb = new()
            {
                Text = onOff,
                Act = () =>
                {
                    component.HushAlerts = !component.HushAlerts;
                    _popupSystem.PopupCoordinates(
                        Loc.GetString(popupText),
                        Transform(args.User).Coordinates,
                        Filter.Entities(args.User),
                        true,
                        PopupType.MediumCaution);

                },
                Message = onOffMsg,
            };
            args.Verbs.Add(verb);
        }
        // COYOTE END

        /// <summary>
        /// Returns the gas mixture for the gas analyzer
        /// </summary>
        private void OnAnalyzed(EntityUid uid, GasTankComponent component, GasAnalyzerScanEvent args)
        {
            args.GasMixtures ??= new List<(string, GasMixture?)>();
            args.GasMixtures.Add((Name(uid), component.Air));
        }

        private void OnGasTankPrice(EntityUid uid, GasTankComponent component, ref PriceCalculationEvent args)
        {
            args.Price += _atmosphereSystem.GetPrice(component.Air);
        }
    }
}
