using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes; // Wayfarer
using Content.Shared.Tag; // Wayfarer
using Robust.Shared.Serialization.TypeSerializers.Implementations; // Wayfarer
using Content.Shared.Inventory; // Wayfarer
using Content.Shared.Buckle; // Wayfarer
using Content.Shared.Buckle.Components;
using Content.Shared.Silicons.Borgs.Components; // Wayfarer

namespace Content.Shared.Medical.Healing;

public sealed partial class HealingSystem : EntitySystem // Wayfarer: Added Partial
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly TagSystem _tag = default!; // Wayfarer
    [Dependency] private readonly InventorySystem _inventorySystem = default!; // Wayfarer

    private static readonly ProtoId<TagPrototype> SurgeryToolsTag = "SurgeryTool"; // Wayfarer

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealingComponent, UseInHandEvent>(OnHealingUse);
        SubscribeLocalEvent<HealingComponent, AfterInteractEvent>(OnHealingAfterInteract);
        SubscribeLocalEvent<DamageableComponent, HealingDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(Entity<DamageableComponent> target, ref HealingDoAfterEvent args)
    {

        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp(args.Used, out HealingComponent? healing))
            return;

        if (healing.DamageContainers is not null &&
            target.Comp.DamageContainerID is not null &&
            !healing.DamageContainers.Contains(target.Comp.DamageContainerID.Value))
        {
            return;
        }

        TryComp<BloodstreamComponent>(target, out var bloodstream);

        // Heal some bloodloss damage.
        if (healing.BloodlossModifier != 0 && bloodstream != null)
        {
            var isBleeding = bloodstream.BleedAmount > 0;
            _bloodstreamSystem.TryModifyBleedAmount((target.Owner, bloodstream), healing.BloodlossModifier);
            if (isBleeding != bloodstream.BleedAmount > 0)
            {
                var popup = (args.User == target.Owner)
                    ? Loc.GetString("medical-item-stop-bleeding-self")
                    : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target.Owner, EntityManager)));
                _popupSystem.PopupClient(popup, target, args.User);
            }
        }

        // Restores missing blood
        if (healing.ModifyBloodLevel != 0 && bloodstream != null)
            _bloodstreamSystem.TryModifyBloodLevel((target.Owner, bloodstream), healing.ModifyBloodLevel);

        var healed = _damageable.TryChangeDamage(target.Owner, healing.Damage * _damageable.UniversalTopicalsHealModifier, true, origin: args.Args.User);

        if (healed == null && healing.BloodlossModifier != 0)
            return;

        var total = healed?.GetTotal() ?? FixedPoint2.Zero;

        // Re-verify that we can heal the damage.
        var dontRepeat = false;
        if (TryComp<StackComponent>(args.Used.Value, out var stackComp))
        {
            _stacks.Use(args.Used.Value, 1, stackComp);

            if (_stacks.GetCount(args.Used.Value, stackComp) <= 0)
                dontRepeat = true;
        }
        else if (!_tag.HasTag(args.Used.Value, SurgeryToolsTag)) // Wayfarer: Surgery tools should not be consumed.
        {
            PredictedQueueDel(args.Used.Value);
        }

        if (target.Owner != args.User)
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed {ToPrettyString(target.Owner):target} for {total:damage} damage");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed themselves for {total:damage} damage");
        }

        _audio.PlayPredicted(healing.HealingEndSound, target.Owner, args.User);

        // Logic to determine the whether or not to repeat the healing action
        args.Repeat = HasDamage((args.Used.Value, healing), target) && !dontRepeat;
        args.Handled = true;

        if (!args.Repeat)
        {
            _popupSystem.PopupClient(Loc.GetString("medical-item-finished-using", ("item", args.Used)), target.Owner, args.User);
            return;
        }

        // Update our self heal delay so it shortens as we heal more damage.
        if (args.User == target.Owner)
            args.Args.Delay = healing.Delay * GetScaledHealingPenalty(target.Owner, healing.SelfHealPenaltyMultiplier);
    }

    private bool HasDamage(Entity<HealingComponent> healing, Entity<DamageableComponent> target)
    {
        var damageableDict = target.Comp.Damage.DamageDict;
        var healingDict = healing.Comp.Damage.DamageDict;
        foreach (var type in healingDict)
        {
            if (damageableDict[type.Key].Value > 0)
            {
                return true;
            }
        }

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            // Is ent missing blood that we can restore?
            if (healing.Comp.ModifyBloodLevel > 0
                && _solutionContainerSystem.ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
                && bloodSolution.Volume < bloodSolution.MaxVolume)
            {
                return true;
            }

            // Is ent bleeding and can we stop it?
            if (healing.Comp.BloodlossModifier < 0 && bloodstream.BleedAmount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void OnHealingUse(Entity<HealingComponent> healing, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        if (TryHeal(healing, args.User, args.User, args.User)) // Wayfarer: 4th argument, to surpport surgery tools detecting buckled.
            args.Handled = true;
    }

    private void OnHealingAfterInteract(Entity<HealingComponent> healing, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (TryHeal(healing, args.Target.Value, args.User, args.Target.Value)) // Wayfarer: 4th argument, to surpport surgery tools detecting buckled.
            args.Handled = true;
    }

    private bool TryHeal(Entity<HealingComponent> healing, Entity<DamageableComponent?> target, EntityUid user, EntityUid? targetBuckle = null) // Wayfarer: add optional buckle target
    {
        if (!Resolve(target, ref target.Comp, false))
            return false;

        if (healing.Comp.DamageContainers is not null &&
            target.Comp.DamageContainerID is not null &&
            !healing.Comp.DamageContainers.Contains(target.Comp.DamageContainerID.Value))
        {
            return false;
        }

        if (user != target.Owner && !_interactionSystem.InRangeUnobstructed(user, target.Owner, popup: true))
            return false;

        if (TryComp<StackComponent>(healing, out var stack) && stack.Count < 1)
            return false;

        if (!HasDamage(healing, target!))
        {
            _popupSystem.PopupClient(Loc.GetString("medical-item-cant-use", ("item", healing.Owner)), healing, user);
            return false;
        }

        // Wayfarer: block healing if the damage is a big ouch owie (too severe)
        if (IsHealingThresholdExceeded(healing, target!))
        {
            _popupSystem.PopupClient(Loc.GetString("medical-item-too-severe", ("item", healing.Owner)), healing, user);
            return false;
        }
        // End Wayfarer

        _audio.PlayPredicted(healing.Comp.HealingBeginSound, healing, user);

        var isNotSelf = user != target.Owner;

        if (isNotSelf)
        {
            var msg = Loc.GetString("medical-item-popup-target", ("user", Identity.Entity(user, EntityManager)), ("item", healing.Owner));
            _popupSystem.PopupEntity(msg, target, target, PopupType.Medium);
        }

        var delay = isNotSelf
            ? healing.Comp.Delay
            : healing.Comp.Delay * GetScaledHealingPenalty(target, healing.Comp.SelfHealPenaltyMultiplier);

        // Wayfarer: Surgical Devices delay is affected by whether the patient is on a bed, and the doctors clothes being sterile
        if (_tag.HasTag(healing, SurgeryToolsTag))
        {

            var surgerySpeedModifier = 1 - (GetSurgicalEnvironmentBonus(target, healing, user, targetBuckle) / 10);
            delay = delay * surgerySpeedModifier;
        }
        // End wayfarer

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, delay, new HealingDoAfterEvent(), target, target: target, used: healing)
            {
                // Didn't break on damage as they may be trying to prevent it and
                // not being able to heal your own ticking damage would be frustrating.
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }

    /// <summary>
    /// Scales the self-heal penalty based on the amount of damage taken
    /// </summary>
    /// <param name="ent">Entity we're healing</param>
    /// <param name="mod">Maximum modifier we can have.</param>
    /// <returns>Modifier we multiply our healing time by</returns>
    public float GetScaledHealingPenalty(Entity<DamageableComponent?, MobThresholdsComponent?> ent, float mod)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, false))
            return mod;

        if (!_mobThresholdSystem.TryGetThresholdForState(ent, MobState.Critical, out var amount, ent.Comp2))
            return 1;

        var percentDamage = (float)(ent.Comp1.TotalDamage / amount);
        //basically make it scale from 1 to the multiplier.

        var output = percentDamage * (mod - 1) + 1;
        return Math.Max(output, 1);
    }
    // Wayfarer
    public float GetSurgicalEnvironmentBonus(Entity<DamageableComponent?> target, Entity<HealingComponent> healing, EntityUid user, EntityUid? targetBuckle)
    {
        //generates a score, used for increasing the speed of surgery
        var surgicalEnvironmentPoints = 0.0;
        //Medical gloves
        if (_inventorySystem.TryGetSlotEntity(user, "gloves", out var gloves))
        {
            surgicalEnvironmentPoints += 1; //any gloves are good - but sterile ones are better.
            var userGlovesID = MetaData(gloves.Value).EntityPrototype?.ID;
            if (userGlovesID == "ClothingHandsGlovesNitrile" || userGlovesID == "ClothingHandsGlovesLatex") //Id references instead of adding a new tag is used, to make it easier to strip out this system.
            {
                surgicalEnvironmentPoints += 1;
            }
        }
        //medical mask
        if (_inventorySystem.TryGetSlotEntity(user, "mask", out var mask))
        {
            surgicalEnvironmentPoints += 1; //any mask is good - but sterile ones are better.
            var userMaskID = MetaData(mask.Value).EntityPrototype?.ID;
            if (userMaskID == "ClothingMaskSterile" || userMaskID == "ClothingMaskBreathMedical" || userMaskID == "ClothingMaskBreathMedicalSecurity")
            {
                surgicalEnvironmentPoints += 1;
            }
        }
        //scrubs
        if (_inventorySystem.TryGetSlotEntity(user, "jumpsuit", out var jumpsuit))
        {
            var userJumpsuitID = MetaData(jumpsuit.Value).EntityPrototype?.ID;
            if (userJumpsuitID == "UniformScrubsColorGreen" || userJumpsuitID == "UniformScrubsColorBlue" || userJumpsuitID == "UniformScrubsColorPurple")
            {
                surgicalEnvironmentPoints += 0.5;
            }
        }
        //cap
        if (_inventorySystem.TryGetSlotEntity(user, "head", out var head))
        {
            var userHeadID = MetaData(head.Value).EntityPrototype?.ID;
            if (userHeadID == "ClothingHeadHatSurgcapGreen" || userHeadID == "ClothingHeadHatSurgcapBlue" || userHeadID == "ClothingHeadHatSurgcapPurple")
            {
                surgicalEnvironmentPoints += 0.5;
            }
        }
        //bed
        if (targetBuckle.HasValue && TryComp<BuckleComponent>(targetBuckle.Value, out var buckleComp))
        {
            if (buckleComp.Buckled)
            {
                surgicalEnvironmentPoints += 1; //any bed (or chair) is good - but surgical beds are better.
                if (buckleComp.BuckledTo != null)
                {
                    var bedID = MetaData(buckleComp.BuckledTo.Value).EntityPrototype?.ID;
                    if (bedID == "StasisBed" || bedID == "OperatingTable")
                    {
                        surgicalEnvironmentPoints += 2;
                    }
                }
            }
        }
        //Cyborg - as borgs cant wear clothes, they get seperate bonuses to make medical cyborgs viable. 

        if (TryComp<BorgSwitchableTypeComponent>(user, out var chassis) && chassis is not null)
        {
            surgicalEnvironmentPoints += 2; //any cyborg is as good as a humanoid with non-sterile gloves and a mask
            if (chassis.SelectedBorgType == "medical")
            {
                surgicalEnvironmentPoints += 3; //medical cyborgs are as good as a humanoid with full surgical gear on. 
            }
        }

        return (float)Math.Min(surgicalEnvironmentPoints, 8); //cap it at 8 points, so that surgery cant become instant.
    }
    // End Wayfarer

}
