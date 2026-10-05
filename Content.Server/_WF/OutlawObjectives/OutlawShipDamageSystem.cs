using Content.Server.Administration.Logs;
using Content.Server.Construction;
using Content.Server.Destructible;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Radiation.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._WF.OutlawObjectives;
using Content.Shared.Construction;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Trigger;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._WF.OutlawObjectives;

public sealed class OutlawShipDamageSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly OutlawObjectivePayoutSystem _payout = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly TimeSpan AttackerMemory = TimeSpan.FromMinutes(1);

    // Explosion damage has no origin, so whoever set off the explosive is recorded when it is triggered, before the explosive is deleted.
    private readonly Dictionary<EntityUid, (EntityUid Attacker, TimeSpan Time)> _explosionAttackers = new();

    // Entities the explosion being processed is about to damage, so only their damage is credited to it.
    private readonly HashSet<EntityUid> _exploding = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PhysicsComponent, DamageChangedEvent>(OnDamaged, after: new[] { typeof(DestructibleSystem) });
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnConstructionChanged);
        SubscribeLocalEvent<ExplosiveComponent, TriggerEvent>(OnExplosiveTriggered);
        SubscribeLocalEvent<PhysicsComponent, BeforeExplodeEvent>(OnBeforeExplode);
        SubscribeLocalEvent<RadiationBlockerComponent, ConstructionInteractDoAfterEvent>(OnWallInteracted);
    }

    private void OnExplosiveTriggered(Entity<ExplosiveComponent> ent, ref TriggerEvent args)
    {
        if (Count<OutlawShipDamageComponent>() == 0)
            return;

        var attacker = TryComp<ProjectileComponent>(ent, out var projectile) ? projectile.Shooter : args.User;
        if (attacker is null)
            return;

        var now = _timing.CurTime;

        if (_explosionAttackers.Count > 64)
        {
            foreach (var (cause, entry) in _explosionAttackers)
            {
                if (now - entry.Time > AttackerMemory)
                    _explosionAttackers.Remove(cause);
            }
        }

        _explosionAttackers[ent] = (attacker.Value, now);
    }

    private void OnBeforeExplode(EntityUid uid, PhysicsComponent physics, ref BeforeExplodeEvent args)
    {
        if (Count<OutlawShipDamageComponent>() == 0 || physics.BodyType != BodyType.Static)
            return;

        if (_exploding.Count > 256)
            _exploding.Clear();

        _exploding.Add(uid);
    }

    private EntityUid? ExplosionAttacker(EntityUid uid)
    {
        if (!_exploding.Remove(uid))
            return null;

        return _explosion.ActiveExplosionCause is { } cause && _explosionAttackers.TryGetValue(cause, out var entry)
            ? entry.Attacker
            : null;
    }

    private void OnDamaged(EntityUid uid, PhysicsComponent physics, DamageChangedEvent args)
    {
        if (Count<OutlawShipDamageComponent>() == 0 || !args.DamageIncreased)
            return;

        // Taken out of the set on every hit, not only fatal ones, so a wall that survived a blast is not left in it.
        if ((args.Origin ?? ExplosionAttacker(uid)) is not { } origin
            || physics.BodyType != BodyType.Static
            || !EntityManager.IsQueuedForDeletion(uid)
            || !HasComp<DestructibleComponent>(uid)
            || Transform(uid).GridUid is not { } grid
            || !TryComp<OutlawShipDamageComponent>(grid, out var damage))
        {
            return;
        }

        Credit(grid, damage, origin);
    }

    // A construction step on a wall of a tracked ship, recorded so the deconstruction that follows can be credited.
    private void OnWallInteracted(Entity<RadiationBlockerComponent> ent, ref ConstructionInteractDoAfterEvent args)
    {
        if (Count<OutlawShipDamageComponent>() == 0
            || args.Cancelled
            || Transform(ent).GridUid is not { } grid
            || !HasComp<OutlawShipDamageComponent>(grid))
        {
            return;
        }

        EnsureComp<LastConstructionUserComponent>(ent).User = args.User;
    }

    // Deconstruction. The new entity is checked so upgrading a wall doesn't count.
    private void OnConstructionChanged(ConstructionChangeEntityEvent args)
    {
        if (Count<OutlawShipDamageComponent>() == 0
            || !TryComp<LastConstructionUserComponent>(args.Old, out var last)
            || Transform(args.Old).GridUid is not { } grid
            || !TryComp<OutlawShipDamageComponent>(grid, out var damage)
            || !_tag.HasTag(args.Old, WallTag)
            || _tag.HasTag(args.New, WallTag))
        {
            return;
        }

        Credit(grid, damage, last.User);
    }

    // Anchored and has health: walls, windows, airlocks, thrusters, gyroscopes, consoles, machines, furniture.
    private bool Counts(EntityUid uid, PhysicsComponent physics)
    {
        return physics.BodyType == BodyType.Static && HasComp<DestructibleComponent>(uid);
    }

    private void Credit(EntityUid grid, OutlawShipDamageComponent damage, EntityUid origin)
    {
        if (!_payout.TryGetSettings(out var settings)
            || ResolveOutlaw(grid, damage, origin, settings) is not { } outlaw
            || outlaw == damage.Target)
        {
            return;
        }

        var credit = damage.Credit.GetValueOrDefault(outlaw) + 1;
        damage.Credit[outlaw] = credit;

        if (credit < damage.Needed)
            return;

        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(outlaw):outlaw} damaged {credit} of {damage.Total} on {ToPrettyString(grid):ship}");

        if (!TryComp<OutlawObjectiveComponent>(damage.Target, out var comp))
            return;

        // Completing the objective removes the damage count from every ship the target owns, so nothing is removed here.
        _payout.Complete(damage.Target, comp, damage.Objective, outlaw);
    }

    private EntityUid? ResolveOutlaw(EntityUid victimGrid, OutlawShipDamageComponent damage, EntityUid origin, OutlawObjectiveSettingsComponent settings)
    {
        if (TerminatingOrDeleted(origin))
            return null;

        if (_payout.IsOutlaw(origin, settings))
            return origin;

        if (Transform(origin).GridUid is not { } gunGrid
            || gunGrid == victimGrid
            || !TryComp<ShuttleDeedComponent>(gunGrid, out var gunDeed)
            || gunDeed.ShuttleOwner is not { } owner)
        {
            return null;
        }

        if (damage.GunOwners.TryGetValue(gunGrid, out var cached)
            && !TerminatingOrDeleted(cached)
            && _payout.IsOutlaw(cached, settings))
        {
            return cached;
        }

        var query = EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (Name(uid) != owner || !_payout.IsOutlaw(uid, settings))
                continue;

            damage.GunOwners[gunGrid] = uid;
            return uid;
        }

        return null;
    }

    public void TrackAll(EntityUid target, ProtoId<OutlawObjectivePrototype> objective)
    {
        foreach (var grid in _payout.ShipsOwnedBy(Name(target)))
            Track(target, objective, grid);
    }

    public void Track(EntityUid target, ProtoId<OutlawObjectivePrototype> objective, EntityUid grid)
    {
        if (HasComp<OutlawShipDamageComponent>(grid) || !_payout.TryGetSettings(out var settings))
            return;

        var total = 0;
        var physicsQuery = GetEntityQuery<PhysicsComponent>();

        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (physicsQuery.TryGetComponent(child, out var physics) && Counts(child, physics))
                total++;
        }

        var damage = AddComp<OutlawShipDamageComponent>(grid);
        damage.Target = target;
        damage.Objective = objective;
        damage.Total = total;
        damage.Needed = Math.Max(1, (int)MathF.Ceiling(settings.ShipDamageFraction * total));
    }

    public void Untrack(EntityUid target, ProtoId<OutlawObjectivePrototype> objective)
    {
        var query = EntityQueryEnumerator<OutlawShipDamageComponent>();
        while (query.MoveNext(out var grid, out var damage))
        {
            if (damage.Target == target && damage.Objective == objective)
                RemCompDeferred<OutlawShipDamageComponent>(grid);
        }
    }
}
