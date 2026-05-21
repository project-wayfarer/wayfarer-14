using Content.Server.Popups;
using Content.Shared._WF.Clown;
using Content.Shared._WF.Traits;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.Clown;

public sealed class BalloonTwistingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly VerbCategory TwistCategory = new("balloon-twist-verb-category", null);

    private static readonly (string Label, EntProtoId Proto)[] Shapes =
    [
        ("balloon-twist-shape-dog",    new EntProtoId("BalloonAnimalDog")),
        ("balloon-twist-shape-clown",  new EntProtoId("BalloonAnimalClown")),
        ("balloon-twist-shape-banana", new EntProtoId("BalloonAnimalBanana")),
        ("balloon-twist-shape-cat",    new EntProtoId("BalloonAnimalCat")),
        ("balloon-twist-shape-moth",   new EntProtoId("BalloonAnimalMoth")),
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BalloonEmptyComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BalloonEmptyComponent, BalloonTwistDoAfterEvent>(OnDoAfter);
    }

    private void OnGetVerbs(Entity<BalloonEmptyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!HasComp<ClownTrainingComponent>(args.User))
            return;

        var user = args.User;
        var uid = ent.Owner;

        foreach (var (label, proto) in Shapes)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(label),
                Category = TwistCategory,
                Act = () => StartTwist(uid, user, proto),
                Priority = 1,
            });
        }
    }

    private void StartTwist(EntityUid uid, EntityUid user, EntProtoId proto)
    {
        var ev = new BalloonTwistDoAfterEvent { TargetPrototype = proto };
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(3), ev, uid, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnDoAfter(Entity<BalloonEmptyComponent> ent, ref BalloonTwistDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var user = args.User;

        // Spawn at the user, not at the held balloon. A held item is attached to its holder,
        // so spawning there would stick the new balloon to the user.
        var coords = Transform(user).Coordinates;

        // Make the new balloon animal, then remove the empty one and put the new one in hand.
        var newItem = Spawn(args.TargetPrototype, coords);

        // Take the empty balloon out of the hand before deleting it. Deletion is delayed, so the
        // hand would otherwise still count as full and block the pickup below.
        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
            _containers.Remove(ent.Owner, container, force: true);
        QueueDel(ent);

        _hands.TryPickupAnyHand(user, newItem);

        _popup.PopupEntity(Loc.GetString("balloon-twist-success"), user);
        args.Handled = true;
    }
}
