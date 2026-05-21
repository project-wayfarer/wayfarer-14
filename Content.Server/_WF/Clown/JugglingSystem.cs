using System.Linq;
using Content.Shared._WF.Clown;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Gravity;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Server._WF.Clown;

public sealed class JugglingSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;

    private const string JuggleContainerId = "juggle";
    private const string NoGravityMsg = "juggling-no-gravity";
    private const int MaxJuggledItems = 10;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JugglingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<JugglingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<JugglingComponent, JuggleActionEvent>(OnJuggle);

        SubscribeLocalEvent<JugglingActiveComponent, ComponentInit>(OnActiveInit);
        SubscribeLocalEvent<JugglingActiveComponent, ComponentShutdown>(OnActiveShutdown);

        // While the player is juggling, ignore the walk-toggle key so they stay locked to walking.
        // Without this, pressing it would switch them back to running.
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Walk, new JuggleWalkBlocker(), typeof(SharedMoverController))
            .Register<JugglingSystem>();

        SubscribeLocalEvent<JugglingActiveComponent, DidEquipHandEvent>(OnDidEquipHand);
        SubscribeLocalEvent<JugglingActiveComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<JugglingActiveComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<JugglingActiveComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<JugglingActiveComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<GravityChangedEvent>(OnGravityChanged);
    }

    private void OnMapInit(Entity<JugglingComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.JuggleAction, ent.Comp.JuggleActionId);
    }

    private void OnShutdown(Entity<JugglingComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.JuggleAction);
    }

    private void OnJuggle(Entity<JugglingComponent> ent, ref JuggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<JugglingActiveComponent>(ent))
            StopJuggling(ent);
        else
            StartJuggling(ent);

        args.Handled = true;
    }

    private void StartJuggling(EntityUid uid)
    {
        var held = _hands.EnumerateHeld(uid).ToList();
        if (held.Count < 2)
            return;

        // Items would not follow a juggle pattern without gravity.
        if (_gravity.IsWeightless(uid))
        {
            _popup.PopupEntity(Loc.GetString(NoGravityMsg), uid, uid);
            return;
        }

        // Items in the hidden "juggle" container are not in hands, so the player cannot use them, attack with them, or pass them.
        var container = _containers.EnsureContainer<Container>(uid, JuggleContainerId);
        var active = AddComp<JugglingActiveComponent>(uid);
        active.StartTime = _timing.CurTime;

        foreach (var item in held)
        {
            if (active.JuggledItems.Count >= MaxJuggledItems)
                break;

            if (_containers.TryGetContainingContainer(item, out var handContainer))
                _containers.Remove(item, handContainer, force: true);

            // Stored as NetEntity so the client can resolve each item locally.
            if (_containers.Insert(item, container))
                active.JuggledItems.Add(GetNetEntity(item));
        }

        // Send the juggling state to every client so other players see the items in the air.
        Dirty(uid, active);

        _popup.PopupEntity(Loc.GetString("juggling-action-popup"), uid, uid);
    }

    private void StopJuggling(EntityUid uid)
    {
        if (!HasComp<JugglingActiveComponent>(uid))
            return;

        // Items with no free hand land on the floor, which is Remove's default behaviour.
        if (_containers.TryGetContainer(uid, JuggleContainerId, out var container))
        {
            foreach (var item in container.ContainedEntities.ToList())
            {
                _containers.Remove(item, container);
                _hands.TryPickupAnyHand(uid, item);
            }
        }

        RemComp<JugglingActiveComponent>(uid);
    }

    // Server half of the forced walk. The player is put into walk mode when
    // juggling starts. The client half is in JugglingVisualsSystem.
    private void OnActiveInit(Entity<JugglingActiveComponent> ent, ref ComponentInit args)
    {
        if (TryComp<InputMoverComponent>(ent.Owner, out var mover))
            _mover.SetSprinting((ent.Owner, mover), 0, true);
    }

    // Server half of the forced walk. The player returns to normal running when
    // juggling ends.
    private void OnActiveShutdown(Entity<JugglingActiveComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<InputMoverComponent>(ent.Owner, out var mover))
            _mover.SetSprinting((ent.Owner, mover), 0, false);
    }

    private void OnDidEquipHand(Entity<JugglingActiveComponent> ent, ref DidEquipHandEvent args)
    {
        var item = args.Equipped;
        var netItem = GetNetEntity(item);

        // An item already being juggled can pass back through the hand for a moment while it is
        // being moved into the hidden container, so ignore items that are already in the rotation.
        if (ent.Comp.JuggledItems.Contains(netItem))
            return;

        if (ent.Comp.JuggledItems.Count >= MaxJuggledItems)
            return;

        var container = _containers.EnsureContainer<Container>(ent.Owner, JuggleContainerId);

        if (_containers.TryGetContainingContainer(item, out var handContainer))
            _containers.Remove(item, handContainer, force: true);

        if (_containers.Insert(item, container))
        {
            ent.Comp.JuggledItems.Add(netItem);
            Dirty(ent, ent.Comp);
        }
    }

    private void OnDamaged(Entity<JugglingActiveComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta != null && args.DamageDelta.GetTotal() > FixedPoint2.Zero)
            StopJuggling(ent.Owner);
    }

    private void OnMobState(Entity<JugglingActiveComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            StopJuggling(ent.Owner);
    }

    // Slipping, stuns, paralysed legs all raise DownedEvent.
    private void OnDowned(Entity<JugglingActiveComponent> ent, ref DownedEvent args)
        => StopJuggling(ent.Owner);

    // Shared by the two ways a juggling clown becomes weightless.
    private void StopWeightless(EntityUid uid)
    {
        _popup.PopupEntity(Loc.GetString(NoGravityMsg), uid, uid);
        StopJuggling(uid);
    }

    // Catches the case of a clown walking off a gravity grid mid-juggle.
    private void OnParentChanged(Entity<JugglingActiveComponent> ent, ref EntParentChangedMessage args)
    {
        if (_gravity.IsWeightless(ent.Owner))
            StopWeightless(ent.Owner);
    }

    // Catches the case of grid gravity being lost while a clown stands on it.
    private void OnGravityChanged(ref GravityChangedEvent ev)
    {
        if (ev.HasGravity)
            return;

        var query = EntityQueryEnumerator<JugglingActiveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != ev.ChangedGridIndex)
                continue;

            StopWeightless(uid);
        }
    }
}
