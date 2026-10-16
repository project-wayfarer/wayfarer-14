using Content.Server.Chat;
using Content.Server.Electrocution;
using Content.Server.Emp;
using Content.Server.Lightning;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Shared._WF.IPC;
using Content.Shared.Bed.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Electrocution;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Lock;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Repairable;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using System.Linq;

namespace Content.Server._WF.IPC;

public sealed class RobotBodySystem : SharedRobotBodySystem
{
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly EncryptionKeySystem _encryptionKey = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RobotBodyComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<RobotBodyComponent, SuicideEvent>(OnSuicide, before: new[] { typeof(SuicideSystem) });
        SubscribeLocalEvent<RobotBodyComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<RobotBodyComponent, RobotBodyRebootDoAfterEvent>(OnRebootDoAfter);
        SubscribeLocalEvent<RobotBodyComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RobotBodyComponent, ElectrocutedEvent>(OnElectrocuted);
        SubscribeLocalEvent<RobotBodyComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<RobotBodyComponent, LockToggledEvent>(OnLockToggled);
        SubscribeLocalEvent<RobotBodyComponent, LockToggleAttemptEvent>(OnLockToggleAttempt);
        SubscribeLocalEvent<RobotBodyComponent, RepairFinishedEvent>(OnRepairFinished, after: new[] { typeof(RepairableSystem) });
        SubscribeLocalEvent<RobotBodyComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnBeforeDamageChanged(Entity<RobotBodyComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.BlockBedHealing && HasComp<HealOnBuckleComponent>(args.Origin))
            args.Cancelled = true;
    }

    private void OnSuicide(Entity<RobotBodyComponent> ent, ref SuicideEvent args)
    {
        if (ent.Comp.SuicideDamageType is { } damageType)
            args.DamageType ??= damageType;
    }

    private void OnGetVerbs(Entity<RobotBodyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ent.Comp.CanReboot
            || !args.CanAccess
            || !args.CanInteract
            || args.Hands == null
            || !_mobState.IsDead(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartReboot(user, ent),
            Text = Loc.GetString("robot-body-reboot-verb"),
            Icon = ent.Comp.RebootVerbIcon,
            Priority = 1,
        });
    }

    private void StartReboot(EntityUid user, Entity<RobotBodyComponent> ent)
    {
        _audio.PlayPvs(ent.Comp.RebootButtonSound, ent);

        var args = new DoAfterArgs(EntityManager, user, ent.Comp.RebootDelay, new RobotBodyRebootDoAfterEvent(), ent, ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnRebootDoAfter(Entity<RobotBodyComponent> ent, ref RobotBodyRebootDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled
            || !TryComp<MobStateComponent>(ent, out var mobState)
            || !_mobState.IsDead(ent, mobState)
            || !TryComp<DamageableComponent>(ent, out var damageable)
            || !_mobThreshold.TryGetThresholdForState(ent, MobState.Critical, out var critical))
            return;

        args.Handled = true;

        if (damageable.TotalDamage < critical)
        {
            _mobState.ChangeMobState(ent, MobState.Alive, mobState);
            return;
        }

        _audio.PlayPvs(ent.Comp.RebootFailSound, ent);
        _popup.PopupEntity(Loc.GetString("robot-body-reboot-failed", ("target", ent.Owner)), ent);
        SpawnSparks(ent);
    }

    private void OnMobStateChanged(Entity<RobotBodyComponent> ent, ref MobStateChangedEvent args)
    {
        if (!ent.Comp.CanReboot || args.OldMobState != MobState.Dead || args.NewMobState != MobState.Alive)
            return;

        _popup.PopupEntity(Loc.GetString("robot-body-reboot-success", ("target", ent.Owner)), ent);
        _audio.PlayPvs(ent.Comp.RebootSound, ent);
    }

    private void OnElectrocuted(Entity<RobotBodyComponent> ent, ref ElectrocutedEvent args)
    {
        if (!ent.Comp.DischargeWhenShockedDead
            || !_mobState.IsDead(ent)
            || !_powerCell.TryGetBatteryFromSlot(ent, out var cell, out var battery)
            || battery.CurrentCharge <= 0)
            return;

        _lightning.ShootRandomLightnings(ent, ent.Comp.DischargeRange, ent.Comp.DischargeBolts);
        _battery.SetCharge(cell.Value, 0, battery);
    }

    private void OnEmpPulse(Entity<RobotBodyComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.EmpShockDamage <= 0)
            return;

        _electrocution.TryDoElectrocution(ent, null, ent.Comp.EmpShockDamage, ent.Comp.EmpShockTime, true, ignoreInsulation: true);
    }

    private void OnLockToggled(Entity<RobotBodyComponent> ent, ref LockToggledEvent args)
    {
        if (!ent.Comp.KeysFollowLock || !TryComp<EncryptionKeyHolderComponent>(ent, out var keyHolder))
            return;

        keyHolder.KeysUnlocked = !args.Locked;
        _encryptionKey.UpdateChannels(ent, keyHolder);
    }

    private void OnLockToggleAttempt(Entity<RobotBodyComponent> ent, ref LockToggleAttemptEvent args)
    {
        if (!ent.Comp.WarnOnPanelTamper || args.Silent || args.User == ent.Owner)
            return;

        _popup.PopupEntity(Loc.GetString("robot-body-panel-tamper", ("user", Identity.Entity(args.User, EntityManager))), ent, ent, PopupType.Large);
    }

    private void OnRepairFinished(Entity<RobotBodyComponent> ent, ref RepairFinishedEvent args)
    {
        if (!ent.Comp.RepeatRepair
            || args.Cancelled
            || args.Used is not { } tool
            || !TryComp<RepairableComponent>(ent, out var repairable)
            || repairable.Damage is not { } heal
            || !TryComp<DamageableComponent>(ent, out var damageable))
            return;

        if (!heal.DamageDict.Keys.Any(type => damageable.Damage.DamageDict.TryGetValue(type, out var amount) && amount > 0))
            return;

        var delay = (float) repairable.DoAfterDelay;
        if (args.User == ent.Owner)
            delay *= repairable.SelfRepairPenalty;

        _tool.UseTool(tool, args.User, ent, delay, repairable.QualityNeeded, new RepairFinishedEvent(), repairable.FuelCost);
    }

    private void OnPlayerSpawnComplete(EntityUid uid, RobotBodyComponent comp, PlayerSpawnCompleteEvent args)
    {
        if (comp.KeySourceSlot is not { } slot
            || !HasComp<EncryptionKeyHolderComponent>(uid)
            || !_inventory.TryGetSlotEntity(uid, slot, out var source)
            || !TryComp<EncryptionKeyHolderComponent>(source, out var sourceKeys))
            return;

        foreach (var key in sourceKeys.KeyContainer.ContainedEntities)
        {
            TrySpawnInContainer(Prototype(key)?.ID, uid, EncryptionKeyHolderComponent.KeyContainerName, out _);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RobotBodyComponent, MobStateComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobState, out var damageable))
        {
            if (comp.BuzzDamageFraction <= 0
                || _mobState.IsDead(uid, mobState)
                || !_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var critical)
                || damageable.TotalDamage < critical * comp.BuzzDamageFraction)
                continue;

            comp.BuzzAccumulator += TimeSpan.FromSeconds(frameTime);
            if (comp.BuzzAccumulator < comp.BuzzCheckInterval)
                continue;

            comp.BuzzAccumulator -= comp.BuzzCheckInterval;

            if (_timing.CurTime < comp.NextBuzz)
                continue;

            comp.NextBuzz = _timing.CurTime + comp.BuzzCooldown;
            _popup.PopupEntity(Loc.GetString("robot-body-buzz"), uid);
            _audio.PlayPvs(comp.BuzzSound, uid);
            SpawnSparks((uid, comp));
        }
    }

    private void SpawnSparks(Entity<RobotBodyComponent> ent)
    {
        if (ent.Comp.SparkEffect is { } effect)
            Spawn(effect, Transform(ent).Coordinates);
    }
}
