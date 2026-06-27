using Content.Server.Radio.EntitySystems;
using Content.Server.Pinpointer;
using Content.Shared.Mobs.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Server.GameObjects; // Frontier
using Content.Server.Station.Systems; // Frontier
using Content.Shared.Humanoid; // Frontier

using System.Threading; // Wayfarer
using Content.Shared.Mobs; // wayfarer
using Content.Shared.Radio; // Wayfarer
using Timer = Robust.Shared.Timing.Timer; // Wayfarer

namespace Content.Server.Trigger.Systems;

public sealed class RattleOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly TransformSystem _transform = default!; // Frontier
    [Dependency] private readonly StationSystem _station = default!; // Frontier

    private readonly Dictionary<EntityUid, CancellationTokenSource> _stillDeadTimers = new(); // Wayfarer

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RattleOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<RattleOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        if (!TryComp<MobStateComponent>(target.Value, out var mobstate))
            return;

        args.Handled = true;

        if (!ent.Comp.Messages.TryGetValue(mobstate.CurrentState, out var messageId))
            return;

        // Frontier: more specific species, grid and coordinate messages

        // // Gets the location of the user
        // var posText = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(target.Value));
        // var message = Loc.GetString(messageId, ("user", target.Value), ("position", posText));

        // Gets location of the implant
        var pos = _transform.GetMapCoordinates(target.Value);
        var x = (int)pos.X;
        var y = (int)pos.Y;
        var posText = $"({x}, {y})";

        // Frontier: Gets station location of the implant
        var station = _station.GetOwningStation(ent);
        var stationText = station is null ? "" : $"{Name(station.Value)} ";

        // Frontier: Gets species of the implant user
        var speciesText = $"";
        if (TryComp<HumanoidAppearanceComponent>(target.Value, out var species))
            speciesText = $" ({species!.Species})";

        var message = Loc.GetString(messageId, ("user", target.Value), ("specie", speciesText), ("grid", stationText!), ("position", posText));
        // End Frontier

        // Sends a message to the radio channel specified by the implant
        _radio.SendRadioMessage(ent.Owner, message, _prototypeManager.Index(ent.Comp.RadioChannel), ent.Owner);

        if (mobstate.CurrentState == MobState.Dead) // Wayfarer: Start the "still dead" timer if the user is, well, still dead.
            StartStillDeadTimer(target.Value, ent.Comp.RadioChannel, ent.Comp.RattleRefireDelay);
    }

    // Wayfarer Start
    private void StartStillDeadTimer(EntityUid entity, ProtoId<RadioChannelPrototype> channel, TimeSpan delay)
    {
        if (_stillDeadTimers.TryGetValue(entity, out var existingCts))
            existingCts.Cancel();

        var cts = new CancellationTokenSource();
        _stillDeadTimers[entity] = cts;

        // Repeating timer callback, gee golly I hope this doesn't come back said the timer
        void TimerCallback()
        {
            if (cts.Token.IsCancellationRequested)
                return;

            if (!EntityManager.EntityExists(entity) ||
                !TryComp<MobStateComponent>(entity, out var mobState) ||
                mobState.CurrentState != MobState.Dead)
            {
                _stillDeadTimers.Remove(entity);
                return;
            }

            // Build and send the "still dead" message, kinda like coyote's.
            var mapPos = _transform.GetMapCoordinates(entity);
            var posText = $"({(int)mapPos.X}, {(int)mapPos.Y})";

            var station = _station.GetOwningStation(entity);
            var stationText = station is null ? "" : $"{Name(station.Value)} ";

            var speciesText = "";
            if (TryComp<HumanoidAppearanceComponent>(entity, out var species))
                speciesText = $" ({species.Species})";

            var message = Loc.GetString("rattle-on-trigger-dead-message-still",
                ("user", entity),
                ("specie", speciesText),
                ("grid", stationText!),
                ("position", posText));

            _radio.SendRadioMessage(entity, message, _prototypeManager.Index(channel), entity);

            // Schedule the next reminder
            Timer.Spawn(delay, TimerCallback, cts.Token);
        }

        Timer.Spawn(delay, TimerCallback, cts.Token);
    }

    public override void Shutdown() // Keeps it from causing an error just in case an entity is deleted with an active timer and rattle.
    {
        base.Shutdown();
        foreach (var cts in _stillDeadTimers.Values)
            cts.Cancel();
        _stillDeadTimers.Clear();
    }
    // Wayfarer End
}
