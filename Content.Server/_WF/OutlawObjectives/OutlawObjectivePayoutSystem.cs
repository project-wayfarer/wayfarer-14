using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._NF.CryoSleep;
using Content.Shared._NF.Bank;
using Content.Server._NF.SectorServices;
using Content.Server._NF.Shipyard.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.StationEvents.Components;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Roles.Components;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Stack;
using Content.Shared._WF.OutlawObjectives;
using Content.Shared.Body.Events;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FloofStation;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._WF.OutlawObjectives;

[ByRefEvent]
public readonly record struct OutlawObjectivesChangedEvent;

[ByRefEvent]
public readonly record struct OutlawObjectiveActivatedEvent;

/// <summary>
/// Sent to an item a contraband pad has taken, before the pad deletes it.
/// </summary>
[ByRefEvent]
public readonly record struct ContrabandSoldEvent(EntityUid Seller);

public sealed class OutlawObjectivePayoutSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly OutlawShipDamageSystem _shipDamage = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    private TimeSpan _nextCheck;

    private readonly List<ProtoId<OutlawObjectivePrototype>> _activatable = new();

    private int _activeShipTheft;
    private int _activeShipDamage;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        SubscribeLocalEvent<OutlawObjectiveComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<OutlawObjectiveComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<OutlawObjectiveComponent, BeingGibbedEvent>(OnGibbed);
        SubscribeLocalEvent<OutlawObjectiveComponent, EntityTerminatingEvent>(OnTargetTerminating);
        SubscribeLocalEvent<OutlawObjectiveComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<OutlawObjectiveComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<OutlawObjectiveComponent, CryosleepEnterEvent>(OnCryosleepEnter);
        SubscribeLocalEvent<OutlawObjectiveComponent, CryosleepWakeUpEvent>(OnCryosleepWakeUp);
        SubscribeLocalEvent<OutlawObjectiveComponent, ComponentShutdown>(OnObjectivesShutdown);

        SubscribeLocalEvent<OutlawObjectiveItemComponent, ContrabandSoldEvent>(OnObjectiveItemSold);
        SubscribeLocalEvent<OutlawObjectiveItemComponent, EntityTerminatingEvent>(OnObjectiveItemTerminating);

        SubscribeLocalEvent<ShuttleConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnShuttleConsoleGetVerbs);
        SubscribeLocalEvent<ShuttleConsoleComponent, ForgeDeedDoAfterEvent>(OnForgeDeed);
        SubscribeLocalEvent<ShuttleDeedComponent, ComponentShutdown>(OnDeedRemoved);
        SubscribeLocalEvent<LinkedLifecycleGridParentComponent, ComponentStartup>(OnShipPurchased);
    }

    private void OnObjectivesShutdown(Entity<OutlawObjectiveComponent> ent, ref ComponentShutdown args)
    {
        foreach (var objective in ent.Comp.Active)
            OnDeactivated(ent, objective);
    }

    private void OnActivated(EntityUid target, ProtoId<OutlawObjectivePrototype> objective)
    {
        switch (_proto.Index(objective).Trigger)
        {
            case OutlawObjectiveTrigger.ShipSold:
                _activeShipTheft++;
                break;
            case OutlawObjectiveTrigger.ShipDamaged:
                _activeShipDamage++;
                _shipDamage.TrackAll(target, objective);
                break;
        }
    }

    private void OnDeactivated(EntityUid target, ProtoId<OutlawObjectivePrototype> objective)
    {
        switch (_proto.Index(objective).Trigger)
        {
            case OutlawObjectiveTrigger.ShipSold:
                _activeShipTheft--;
                break;
            case OutlawObjectiveTrigger.ShipDamaged:
                _activeShipDamage--;
                _shipDamage.Untrack(target, objective);
                break;
        }
    }

    private bool Deactivate(EntityUid target, OutlawObjectiveComponent comp, ProtoId<OutlawObjectivePrototype> objective)
    {
        if (!comp.Active.Remove(objective))
            return false;

        OnDeactivated(target, objective);
        return true;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // The objectives are traits, so a job that ignores trait choices ignores these too.
        if (HasComp<InterviewHologramComponent>(args.Mob)
            || args.JobId is not { } jobId
            || !_proto.TryIndex<JobPrototype>(jobId, out var job)
            || !job.ApplyTraits
            || !TryGetSettings(out var settings))
        {
            return;
        }

        OutlawObjectiveComponent? comp = null;

        foreach (var objective in _proto.EnumeratePrototypes<OutlawObjectivePrototype>())
        {
            if (!args.Profile.TraitPreferences.Contains(objective.Trait)
                || comp is not null && ConflictsWithTaken(objective, comp))
            {
                continue;
            }

            comp ??= EnsureComp<OutlawObjectiveComponent>(args.Mob);
            comp.Pending.Add(objective.ID);

            if (objective.Item is not { } item)
                continue;

            comp.Items[objective.ID] = GiveObjectiveItem(args.Mob, objective.ID, item, settings);
        }

        if (comp is null)
            return;

        EnsureComp<OutlawObjectivePendingComponent>(args.Mob).NextActivation = NextActivationTime(settings);
        RefreshApp();
    }

    // A profile saved before two objectives excluded each other can still carry both boxes.
    private bool ConflictsWithTaken(OutlawObjectivePrototype objective, OutlawObjectiveComponent comp)
    {
        foreach (var conflict in _proto.Index(objective.Trait).Conflicts)
        {
            foreach (var taken in comp.Pending)
            {
                if (_proto.Index(taken).Trait == conflict)
                    return true;
            }
        }

        return false;
    }

    private TimeSpan NextActivationTime(OutlawObjectiveSettingsComponent settings)
    {
        return _timing.CurTime + _random.Next(settings.ActivationIntervalMin, settings.ActivationIntervalMax);
    }

    public override void Update(float frameTime)
    {
        if (Count<OutlawObjectivePendingComponent>() == 0
            || _ticker.RunLevel != GameRunLevel.InRound
            || _timing.CurTime < _nextCheck
            || !TryGetSettings(out var settings))
        {
            return;
        }

        _nextCheck = _timing.CurTime + settings.CheckInterval;

        var activatedAny = false;

        var query = EntityQueryEnumerator<OutlawObjectivePendingComponent, OutlawObjectiveComponent>();
        while (query.MoveNext(out var uid, out var pending, out var comp))
        {
            if (_timing.CurTime < pending.NextActivation || IsSsd(uid))
                continue;

            _activatable.Clear();
            bool? hasShip = null;

            foreach (var objective in comp.Pending)
            {
                if (IsShipObjective(objective) && !(hasShip ??= HasShip(Name(uid))))
                    continue;

                _activatable.Add(objective);
            }

            if (_activatable.Count == 0)
            {
                pending.NextActivation = NextActivationTime(settings);
                continue;
            }

            var activated = _random.Pick(_activatable);
            comp.Pending.Remove(activated);
            comp.Active.Add(activated);
            OnActivated(uid, activated);
            activatedAny = true;

            if (comp.Pending.Count == 0)
                RemComp<OutlawObjectivePendingComponent>(uid);
            else
                pending.NextActivation = NextActivationTime(settings);

            if (comp.Items.TryGetValue(activated, out var itemUid) && TryComp<OutlawObjectiveItemComponent>(itemUid, out var item))
                item.Active = true;

            _adminLog.Add(LogType.Action, LogImpact.Low, $"Outlaw objective {activated} activated on {ToPrettyString(uid):target}");
        }

        if (!activatedAny)
            return;

        var ev = new OutlawObjectiveActivatedEvent();
        RaiseLocalEvent(ref ev);
        RefreshApp();
    }

    private EntityUid GiveObjectiveItem(
        EntityUid player,
        ProtoId<OutlawObjectivePrototype> objective,
        EntProtoId item,
        OutlawObjectiveSettingsComponent settings)
    {
        var spawned = Spawn(item, Transform(player).Coordinates);
        var owner = Name(player);

        _metaData.SetEntityName(spawned, Loc.GetString("outlaw-objective-item-name", ("owner", owner)));

        var marker = EnsureComp<OutlawObjectiveItemComponent>(spawned);
        marker.OwningCharacter = player;
        marker.OwnerName = owner;
        marker.Objective = objective;

        if (!TryStow(player, spawned, settings))
            _hands.TryPickupAnyHand(player, spawned, checkActionBlocker: false);

        return spawned;
    }

    private void OnObjectiveItemTerminating(Entity<OutlawObjectiveItemComponent> ent, ref EntityTerminatingEvent args)
    {
        var owner = ent.Comp.OwningCharacter;

        if (!TerminatingOrDeleted(owner) && TryComp<OutlawObjectiveComponent>(owner, out var comp))
        {
            comp.Items.Remove(ent.Comp.Objective);
            Deactivate(owner, comp, ent.Comp.Objective);

            if (comp.Pending.Remove(ent.Comp.Objective) && comp.Pending.Count == 0)
                RemComp<OutlawObjectivePendingComponent>(owner);

            DropIfSettled((owner, comp));
        }

        RefreshApp();
    }

    private void OnObjectiveItemSold(Entity<OutlawObjectiveItemComponent> ent, ref ContrabandSoldEvent args)
    {
        var owner = ent.Comp.OwningCharacter;

        // Handing in your own item is not a theft.
        if (owner == args.Seller
            || !ent.Comp.Active
            || _proto.Index(ent.Comp.Objective).Trigger != OutlawObjectiveTrigger.ItemSold
            || !IsOutlaw(args.Seller))
        {
            return;
        }

        TryComp<OutlawObjectiveComponent>(owner, out var comp);

        Complete(owner, comp, ent.Comp.Objective, args.Seller);
    }

    private void OnMobStateChanged(EntityUid uid, OutlawObjectiveComponent comp, MobStateChangedEvent args)
    {
        // Without this, an outlaw who hurt someone before they were revived is still paid when that body is destroyed later.
        if (args.NewMobState == MobState.Alive)
            comp.LastAttacker = null;

        if (args.NewMobState == MobState.Dead
            && (args.Origin ?? comp.LastAttacker ?? GetPredator(uid)) is { } outlaw
            && outlaw != uid
            && IsOutlaw(outlaw)
            && !IsSsd(uid))
        {
            CompleteAll((uid, comp), OutlawObjectiveTrigger.Killed, outlaw);
        }

        RefreshApp();

        DropIfSettled((uid, comp));
    }

    private void OnDamageChanged(EntityUid uid, OutlawObjectiveComponent comp, DamageChangedEvent args)
    {
        if (args.DamageIncreased && args.Origin is { } attacker)
            comp.LastAttacker = attacker;
    }

    private void OnGibbed(Entity<OutlawObjectiveComponent> ent, ref BeingGibbedEvent args)
    {
        if (FindGibPayee(ent) is { } outlaw)
            CompleteAll(ent, OutlawObjectiveTrigger.Gibbed, outlaw);

        RefreshApp();
    }

    private EntityUid? FindGibPayee(Entity<OutlawObjectiveComponent> target)
    {
        if (!TryGetSettings(out var settings))
            return null;

        if (target.Comp.LastAttacker is { } attacker && attacker != target.Owner && IsOutlaw(attacker, settings))
            return attacker;

        var coordinates = _transform.GetMapCoordinates(target);

        EntityUid? nearest = null;
        var nearestDistanceSquared = float.MaxValue;

        var candidates = _lookup.GetEntitiesInRange<MobStateComponent>(coordinates, settings.GibPayoutRange);

        foreach (var candidate in candidates)
        {
            if (candidate.Owner == target.Owner
                || _mobState.IsDead(candidate, candidate.Comp)
                || !IsOutlaw(candidate, settings))
            {
                continue;
            }

            var distanceSquared = (_transform.GetWorldPosition(candidate) - coordinates.Position).LengthSquared();

            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearest = candidate;
            nearestDistanceSquared = distanceSquared;
        }

        return nearest;
    }

    private void CompleteAll(Entity<OutlawObjectiveComponent> target, OutlawObjectiveTrigger trigger, EntityUid outlaw)
    {
        foreach (var objective in target.Comp.Active.ToArray())
        {
            if (_proto.Index(objective).Trigger == trigger)
                Complete(target.Owner, target.Comp, objective, outlaw);
        }
    }

    public void Complete(EntityUid target, OutlawObjectiveComponent? comp, ProtoId<OutlawObjectivePrototype> objective, EntityUid outlaw)
    {
        if (!TryGetSettings(out var settings))
            return;

        if (comp is not null && !Deactivate(target, comp, objective))
            return;

        var proto = _proto.Index(objective);

        var coordinates = Transform(outlaw).Coordinates;

        GiveReward(outlaw, _stack.Spawn(proto.RewardDC, settings.StackDC, coordinates), settings);
        GiveReward(outlaw, _stack.Spawn(proto.RewardSpesos, settings.StackSpesos, coordinates), settings);

        // Only the outlaw hears this.
        if (TryComp<ActorComponent>(outlaw, out var actor))
        {
            var message = Loc.GetString("outlaw-objectives-completed",
                ("chits", proto.RewardDC),
                ("cash", BankSystemExtensions.ToIndependentString(proto.RewardSpesos)));

            _chat.ChatMessageToOne(ChatChannel.Server, message,
                Loc.GetString("outlaw-objectives-completed-wrap", ("message", FormattedMessage.EscapeText(message))),
                EntityUid.Invalid, false, actor.PlayerSession.Channel, colorOverride: Color.FromHex("#a36b00"));

            _audio.PlayEntity(settings.RewardSound, outlaw, outlaw);
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(outlaw):outlaw} was paid {proto.RewardDC} data chits and {proto.RewardSpesos} spesos for {objective} on {ToPrettyString(target):target}");

        RefreshApp();
    }

    private void GiveReward(EntityUid outlaw, EntityUid reward, OutlawObjectiveSettingsComponent settings)
    {
        if (!_hands.TryPickupAnyHand(outlaw, reward))
            TryStow(outlaw, reward, settings);
    }

    private bool TryStow(EntityUid player, EntityUid item, OutlawObjectiveSettingsComponent settings)
    {
        return _inventory.TryGetSlotEntity(player, settings.ItemSlot, out var container)
               && _storage.Insert(container.Value, item, out _, playSound: false);
    }

    private bool IsShipObjective(ProtoId<OutlawObjectivePrototype> objective)
    {
        return _proto.Index(objective).Trigger is OutlawObjectiveTrigger.ShipSold or OutlawObjectiveTrigger.ShipDamaged;
    }

    public IEnumerable<EntityUid> ShipsOwnedBy(string name)
    {
        var query = EntityQueryEnumerator<ShuttleDeedComponent, MapGridComponent>();
        while (query.MoveNext(out var grid, out var deed, out _))
        {
            if (deed.ShuttleOwner == name)
                yield return grid;
        }
    }

    private bool HasShip(string name)
    {
        foreach (var _ in ShipsOwnedBy(name))
            return true;

        return false;
    }

    public bool TryFindShipTarget(
        string? ownerName,
        OutlawObjectiveTrigger trigger,
        EntityUid? exclude,
        out Entity<OutlawObjectiveComponent> target,
        out ProtoId<OutlawObjectivePrototype> objective)
    {
        target = default;
        objective = default;

        if (ownerName is null)
            return false;

        var query = EntityQueryEnumerator<OutlawObjectiveComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (uid == exclude || Name(uid) != ownerName)
                continue;

            foreach (var active in comp.Active)
            {
                if (_proto.Index(active).Trigger != trigger)
                    continue;

                target = (uid, comp);
                objective = active;
                return true;
            }
        }

        return false;
    }

    private void OnShuttleConsoleGetVerbs(Entity<ShuttleConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (_activeShipTheft == 0
            || !args.CanAccess
            || !args.CanInteract
            || !TryGetSettings(out var settings)
            || !TryFindForgeTarget(ent, args.User, settings, out _, out _, out _))
        {
            return;
        }

        var console = ent.Owner;
        var user = args.User;
        var time = settings.DeedForgeTime;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("outlaw-objectives-forge-deed-verb"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, time, new ForgeDeedDoAfterEvent(), console, used: console)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
            }),
        });
    }

    private bool TryFindForgeTarget(
        EntityUid console,
        EntityUid user,
        OutlawObjectiveSettingsComponent settings,
        out Entity<ShuttleDeedComponent> deed,
        out Entity<OutlawObjectiveComponent> target,
        out ProtoId<OutlawObjectivePrototype> objective)
    {
        deed = default;
        target = default;
        objective = default;

        if (Transform(console).GridUid is not { } grid
            || !TryComp<ShuttleDeedComponent>(grid, out var deedComp)
            || !IsOutlaw(user, settings)
            || !TryFindShipTarget(deedComp.ShuttleOwner, OutlawObjectiveTrigger.ShipSold, user, out target, out objective))
        {
            return false;
        }

        deed = (grid, deedComp);
        return true;
    }

    private void OnForgeDeed(Entity<ShuttleConsoleComponent> ent, ref ForgeDeedDoAfterEvent args)
    {
        if (args.Cancelled
            || args.Handled
            || !TryGetSettings(out var settings)
            || !TryFindForgeTarget(ent, args.User, settings, out var deed, out var target, out var objective))
        {
            return;
        }

        var forged = Spawn(settings.ForgedDeed, Transform(args.User).Coordinates);
        _shipyard.CopyDeed(deed, forged);

        var marker = EnsureComp<ForgedDeedComponent>(forged);
        marker.Forger = args.User;
        marker.Target = target;
        marker.Objective = objective;

        _hands.PickupOrDrop(args.User, forged);

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):outlaw} forged a deed for {ToPrettyString(deed):ship}");
        args.Handled = true;
    }

    private void OnDeedRemoved(Entity<ShuttleDeedComponent> ent, ref ComponentShutdown args)
    {
        if (_activeShipTheft == 0
            || !TryComp<ForgedDeedComponent>(ent, out var forged)
            || TerminatingOrDeleted(ent)
            || TerminatingOrDeleted(forged.Forger)
            || ent.Comp.ShuttleUid is not { } ship
            // If the ship is already gone, the deed was cleaned off a card, not sold.
            || Deleted(ship)
            || !TryGetSettings(out var settings)
            || !_container.TryGetContainingContainer(ent.Owner, out var container)
            || !TryComp<ActivatableUIComponent>(container.Owner, out var ui)
            || ui.Key is not ShipyardConsoleUiKey console
            || !settings.ShipSaleConsoles.Contains(console)
            || !TryComp<OutlawObjectiveComponent>(forged.Target, out var comp))
        {
            return;
        }

        Complete(forged.Target, comp, forged.Objective, forged.Forger);
        QueueDel(ent);
    }

    // Only a bought ship gets this component, and the purchase adds it after naming the deed's owner.
    private void OnShipPurchased(Entity<LinkedLifecycleGridParentComponent> ent, ref ComponentStartup args)
    {
        if (_activeShipDamage == 0
            || !TryComp<ShuttleDeedComponent>(ent, out var deed)
            || !TryFindShipTarget(deed.ShuttleOwner, OutlawObjectiveTrigger.ShipDamaged, null, out var target, out var objective))
        {
            return;
        }

        _shipDamage.Track(target, objective, ent);
    }

    private void OnPlayerAttached(EntityUid uid, OutlawObjectiveComponent comp, PlayerAttachedEvent args) => RefreshApp();

    private void OnPlayerDetached(EntityUid uid, OutlawObjectiveComponent comp, PlayerDetachedEvent args) => RefreshApp();

    private void OnCryosleepEnter(EntityUid uid, OutlawObjectiveComponent comp, CryosleepEnterEvent args) => RefreshApp();

    private void OnCryosleepWakeUp(EntityUid uid, OutlawObjectiveComponent comp, CryosleepWakeUpEvent args) => RefreshApp();

    // A body that is deleted outright never gibs, so this is the last chance to pay for destroying it.
    private void OnTargetTerminating(Entity<OutlawObjectiveComponent> ent, ref EntityTerminatingEvent args)
    {
        if (GetPredator(ent) is { } outlaw && IsOutlaw(outlaw))
            CompleteAll(ent, OutlawObjectiveTrigger.Gibbed, outlaw);

        RefreshApp();
    }

    private EntityUid? GetPredator(EntityUid uid)
    {
        if (TryComp<VoredComponent>(uid, out var vored))
            return vored.Pred;

        return TryComp<HeldInMouthComponent>(uid, out var held) ? held.Pred : null;
    }

    private void DropIfSettled(Entity<OutlawObjectiveComponent> target)
    {
        if (!target.Comp.Settled)
            return;

        RemComp<OutlawObjectiveComponent>(target);
    }

    private void RefreshApp()
    {
        var ev = new OutlawObjectivesChangedEvent();
        RaiseLocalEvent(ref ev);
    }

    private bool IsSsd(EntityUid uid)
    {
        return !HasComp<ActorComponent>(uid);
    }

    private bool IsOutlaw(EntityUid uid)
    {
        return TryGetSettings(out var settings) && IsOutlaw(uid, settings);
    }

    public bool IsOutlaw(EntityUid uid, OutlawObjectiveSettingsComponent settings)
    {
        return _mind.TryGetMind(uid, out var mindId, out var mind)
               && _roles.MindHasRole((mindId, mind), settings.OutlawRoles);
    }

    public bool TryGetSettings([NotNullWhen(true)] out OutlawObjectiveSettingsComponent? settings)
    {
        return TryComp(_sectorService.GetServiceEntity(), out settings);
    }
}
