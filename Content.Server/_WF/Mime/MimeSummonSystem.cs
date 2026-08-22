using Content.Shared._WF.Mime;
using Content.Shared.Abilities.Mime;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._WF.Mime;

public sealed class MimeSummonSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MimeAbilitiesComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MimeAbilitiesComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MimeAbilitiesComponent, BreakVowAlertEvent>(OnBreakVow, after: [typeof(MimePowersSystem)]);
        SubscribeLocalEvent<MimeAbilitiesComponent, RetakeVowAlertEvent>(OnRetakeVow, after: [typeof(MimePowersSystem)]);

        SubscribeLocalEvent<MimeSummonActionComponent, MapInitEvent>(OnActionMapInit);
        SubscribeLocalEvent<MimeSummonActionComponent, MimeSummonActionEvent>(OnSummonAction);

        SubscribeLocalEvent<InvisibleBoxComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<InvisibleBoxComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnMapInit(Entity<MimeAbilitiesComponent> ent, ref MapInitEvent args) => Grant(ent);

    private void OnShutdown(Entity<MimeAbilitiesComponent> ent, ref ComponentShutdown args) => Revoke(ent);

    private void OnBreakVow(Entity<MimeAbilitiesComponent> ent, ref BreakVowAlertEvent args)
    {
        if (!TryComp<MimePowersComponent>(ent, out var powers) || !powers.VowBroken)
            return;

        Revoke(ent);
    }

    private void OnRetakeVow(Entity<MimeAbilitiesComponent> ent, ref RetakeVowAlertEvent args)
    {
        // Retaking the vow does nothing while the mime is still on cooldown.
        if (!TryComp<MimePowersComponent>(ent, out var powers) || powers.VowBroken)
            return;

        Grant(ent);
    }

    private void Grant(Entity<MimeAbilitiesComponent> ent)
    {
        for (var i = 0; i < ent.Comp.Actions.Count; i++)
        {
            EntityUid? action = i < ent.Comp.Granted.Count ? ent.Comp.Granted[i] : null;
            if (!_actions.AddAction(ent.Owner, ref action, ent.Comp.Actions[i]))
                continue;

            if (i < ent.Comp.Granted.Count)
                ent.Comp.Granted[i] = action.Value;
            else
                ent.Comp.Granted.Add(action.Value);
        }
    }

    private void Revoke(Entity<MimeAbilitiesComponent> ent)
    {
        foreach (var action in ent.Comp.Granted)
        {
            // A summoned item cannot be dropped, so it has to go back before its button disappears.
            if (!TerminatingOrDeleted(ent)
                && TryComp<MimeSummonActionComponent>(action, out var summon)
                && summon.Item is { } item
                && _hands.IsHolding(ent.Owner, item))
                Retract((action, summon), ent.Owner, item);

            _actions.RemoveAction(ent.Owner, action);
        }
    }

    private void OnActionMapInit(Entity<MimeSummonActionComponent> ent, ref MapInitEvent args)
    {
        _containers.EnsureContainer<ContainerSlot>(ent.Owner, MimeSummonActionComponent.ContainerId);

        if (TrySpawnInContainer(ent.Comp.ItemId, ent.Owner, MimeSummonActionComponent.ContainerId, out var item))
            ent.Comp.Item = item;
    }

    private void OnSummonAction(Entity<MimeSummonActionComponent> ent, ref MimeSummonActionEvent args)
    {
        if (ent.Comp.Item is not { } item)
            return;

        if (_hands.IsHolding(args.Performer, item))
        {
            Retract(ent, args.Performer, item);
            args.Handled = true;
            return;
        }

        args.Handled = Summon(ent, args.Performer, item);
    }

    private bool Summon(Entity<MimeSummonActionComponent> ent, EntityUid user, EntityUid item)
    {
        // Marking it undroppable before knowing the pickup worked would trap it in the action forever.
        if (!_hands.TryPickupAnyHand(user, item, checkActionBlocker: false))
        {
            _popup.PopupEntity(Loc.GetString("mime-summon-hands-full"), user, user);
            return false;
        }

        EnsureComp<UnremoveableComponent>(item);
        Announce(user, ent.Comp.SummonMessage);
        return true;
    }

    private void Retract(Entity<MimeSummonActionComponent> ent, EntityUid user, EntityUid item)
    {
        RemComp<UnremoveableComponent>(item);
        _containers.Insert(item, _containers.GetContainer(ent.Owner, MimeSummonActionComponent.ContainerId));
        Announce(user, ent.Comp.PutAwayMessage);
    }

    private void OnInserted(Entity<InvisibleBoxComponent> ent, ref EntInsertedIntoContainerMessage args)
        => AnnounceContents(ent, "mime-invisible-box-insert", args.Entity);

    private void OnRemoved(Entity<InvisibleBoxComponent> ent, ref EntRemovedFromContainerMessage args)
        => AnnounceContents(ent, "mime-invisible-box-remove", args.Entity);

    private void AnnounceContents(EntityUid box, string message, EntityUid item)
    {
        var holder = Transform(box).ParentUid;
        if (!_hands.IsHolding(holder, box))
            return;

        _popup.PopupEntity(
            Loc.GetString($"{message}-others", ("user", Identity.Entity(holder, EntityManager)), ("item", item)),
            holder, Filter.PvsExcept(holder), true);
    }

    private void Announce(EntityUid user, string message)
    {
        _popup.PopupEntity(Loc.GetString($"{message}-self"), user, user);
        _popup.PopupEntity(
            Loc.GetString($"{message}-others", ("user", Identity.Entity(user, EntityManager))),
            user, Filter.PvsExcept(user), true);
    }
}
