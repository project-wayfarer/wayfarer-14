using Content.Server.Administration.Logs;
using Content.Shared._WF.IPC;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Whitelist;

namespace Content.Server._WF.IPC;

public sealed class RepairableEyesSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RepairableEyesComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<RepairableEyesComponent, RepairableEyesDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(Entity<RepairableEyesComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !_whitelist.IsWhitelistPass(ent.Comp.Whitelist, args.Used)
            || !TryComp<BlindableComponent>(ent, out var blindable)
            || blindable.EyeDamage <= 0)
            return;

        var delay = ent.Comp.Delay;
        if (args.User == ent.Owner)
            delay *= ent.Comp.SelfRepairMultiplier;

        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new RepairableEyesDoAfterEvent(), ent, ent, args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<RepairableEyesComponent> ent, ref RepairableEyesDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } tool || !TryComp<BlindableComponent>(ent, out var blindable))
            return;

        args.Handled = true;
        _blindable.AdjustEyeDamage((ent, blindable), -blindable.EyeDamage);

        _adminLog.Add(LogType.Healed, $"{ToPrettyString(args.User):user} repaired the eyes of {ToPrettyString(ent):target}");
        _popup.PopupEntity(Loc.GetString("comp-repairable-repair", ("target", ent.Owner), ("tool", tool)), ent, args.User);
    }
}
