using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._NF.CryoSleep;
using Content.Shared._NF.Bank;
using Content.Server._NF.SectorServices;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Stack;
using Content.Shared._WF.OutlawObjectives;
using Content.Shared.Body.Events;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Database;
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
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._WF.OutlawObjectives;

[ByRefEvent]
public readonly record struct OutlawObjectivesChangedEvent;

/// <summary>
/// Raised on an entity a contraband pad has taken before the pad deletes it.
/// </summary>
[ByRefEvent]
public readonly record struct ContrabandSoldEvent(EntityUid Seller);

public sealed class OutlawObjectivePayoutSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StackSystem _stack = default!;

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

        SubscribeLocalEvent<OutlawObjectiveItemComponent, ContrabandSoldEvent>(OnObjectiveItemSold);
        SubscribeLocalEvent<OutlawObjectiveItemComponent, EntityTerminatingEvent>(OnObjectiveItemTerminating);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // The objectives are traits, so a job that ignores trait choices ignores these too.
        if (args.JobId is not { } jobId
            || !_proto.TryIndex<JobPrototype>(jobId, out var job)
            || !job.ApplyTraits
            || !TryGetSettings(out var settings))
        {
            return;
        }

        OutlawObjectiveComponent? comp = null;

        foreach (var objective in _proto.EnumeratePrototypes<OutlawObjectivePrototype>())
        {
            if (!args.Profile.TraitPreferences.Contains(objective.Trait))
                continue;

            comp ??= EnsureComp<OutlawObjectiveComponent>(args.Mob);
            comp.Open.Add(objective.ID);

            if (objective.Item is not { } item)
                continue;

            GiveObjectiveItem(args.Mob, objective.ID, item, settings);
            comp.Items.Add(objective.ID);
        }

        if (comp is not null)
            RefreshApp();
    }

    private void GiveObjectiveItem(
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
    }

    private void OnObjectiveItemTerminating(Entity<OutlawObjectiveItemComponent> ent, ref EntityTerminatingEvent args)
    {
        var owner = ent.Comp.OwningCharacter;

        if (!TerminatingOrDeleted(owner) && TryComp<OutlawObjectiveComponent>(owner, out var comp))
        {
            comp.Items.Remove(ent.Comp.Objective);
            comp.Open.Remove(ent.Comp.Objective);

            DropIfSettled((owner, comp));
        }

        RefreshApp();
    }

    private void OnObjectiveItemSold(Entity<OutlawObjectiveItemComponent> ent, ref ContrabandSoldEvent args)
    {
        var owner = ent.Comp.OwningCharacter;

        // Handing in your own item is not a theft.
        if (owner == args.Seller
            || _proto.Index(ent.Comp.Objective).Trigger != OutlawObjectiveTrigger.ItemSold
            || !IsOutlaw(args.Seller))
        {
            return;
        }

        TryComp<OutlawObjectiveComponent>(owner, out var comp);

        Complete(owner, comp, ent.Comp.Objective, args.Seller);
        RefreshApp();
    }

    private void OnMobStateChanged(EntityUid uid, OutlawObjectiveComponent comp, MobStateChangedEvent args)
    {
        // Without this, an outlaw who hurt someone before they were revived is still paid when that body is destroyed later.
        if (args.NewMobState == MobState.Alive)
            comp.LastAttacker = null;

        if (args.OldMobState == MobState.Alive
            && args.NewMobState is MobState.Critical or MobState.Dead
            && (args.Origin ?? comp.LastAttacker ?? GetPredator(uid)) is { } outlaw
            && outlaw != uid
            && IsOutlaw(outlaw)
            && !IsSsd(uid))
        {
            CompleteAll((uid, comp), OutlawObjectiveTrigger.Critical, outlaw);
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
        foreach (var objective in target.Comp.Open.ToArray())
        {
            if (_proto.Index(objective).Trigger == trigger)
                Complete(target.Owner, target.Comp, objective, outlaw);
        }
    }

    private void Complete(EntityUid target, OutlawObjectiveComponent? comp, ProtoId<OutlawObjectivePrototype> objective, EntityUid outlaw)
    {
        if (!TryGetSettings(out var settings))
            return;

        if (comp is not null && !comp.Open.Remove(objective))
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
        if (target.Comp.Settled)
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

    private bool IsOutlaw(EntityUid uid, OutlawObjectiveSettingsComponent settings)
    {
        return _mind.TryGetMind(uid, out var mindId, out var mind)
               && _roles.MindHasRole((mindId, mind), settings.OutlawRoles);
    }

    private bool TryGetSettings([NotNullWhen(true)] out OutlawObjectiveSettingsComponent? settings)
    {
        return TryComp(_sectorService.GetServiceEntity(), out settings);
    }
}
