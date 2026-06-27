// using Content.Shared.Humanoid; // Wayfarer
using Content.Shared.Alert;
using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Damage;
using Robust.Shared.Timing;
// using Robust.Shared.Prototypes; // Wayfarer
// using Content.Shared.Actions; // Wayfarer
// using Content.Shared.Station; // Wayfarer
// using Content.Shared.Popups; // Wayfarer
// using Content.Shared.Body.Systems; // Wayfarer
// using Content.Shared.Body.Components; // Wayfarer
// using Content.Shared.Inventory; // Wayfarer
// using Content.Shared.Tag; // Wayfarer
// using Robust.Shared.Random; // Wayfarer
// using Content.Server._Starlight.NullSpace; // Wayfarer
// using Content.Server._Starlight.Bluespace; // Wayfarer
// using Content.Server.Stunnable; // Wayfarer
// using Content.Shared.Damage.Systems; // Wayfarer
// using Content.Server.DoAfter; // Wayfarer
// using Content.Shared.Ensnaring; // Wayfarer
// using Robust.Shared.Audio.Systems; // Wayfarer
// using Content.Shared.StatusEffectNew; // Wayfarer
// using Content.Shared.Mobs.Components; // Wayfarer
// using Robust.Shared.Map.Components; // Wayfarer
// using Content.Server.GameTicking; // Wayfarer
// using Content.Server._Starlight.Language; // Wayfarer
// using Content.Shared._Starlight.Medical.Body.Events; // Wayfarer
using Robust.Shared.Containers;
using Content.Server.Examine;
using Robust.Server.GameObjects;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Body.Events;
// using Content.Shared._Starlight.Overlay.Components; // Wayfarer
// using Content.Shared._Starlight.NullSpace.Components; // Wayfarer
// using Content.Server._Starlight.Shadekin.Components; // Wayfarer

namespace Content.Server._Starlight.Shadekin;

public sealed partial class ShadekinSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    // [Dependency] private SharedActionsSystem _actionsSystem = default!; // Wayfarer
    // [Dependency] private SharedStationSystem _station = default!; // Wayfarer
    // [Dependency] private SharedPopupSystem _popup = default!; // Wayfarer
    // [Dependency] private SharedBodySystem _bodySystem = default!; // Wayfarer
    // [Dependency] private InventorySystem _inventorySystem = default!; // Wayfarer
    // [Dependency] private TagSystem _tag = default!; // Wayfarer
    // [Dependency] private SharedMapSystem _mapSystem = default!; // Wayfarer
    // [Dependency] private IRobustRandom _random = default!; // Wayfarer
    // [Dependency] private NullSpacePhaseSystem _nullspace = default!; // Wayfarer
    // [Dependency] private StunSystem _stunSystem = default!; // Wayfarer
    // [Dependency] private DoAfterSystem _doAfterSystem = default!; // Wayfarer
    // [Dependency] private SharedEnsnareableSystem _ensnareable = default!; // Wayfarer
    // [Dependency] private SharedAudioSystem _audio = default!; // Wayfarer
    // [Dependency] private StatusEffectsSystem _status = default!; // Wayfarer
    // [Dependency] private GameTicker _gameTicker = default!; // Wayfarer
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ExamineSystem _examine = default!;
    // [Dependency] private LanguageSystem _language = default!; // Wayfarer

    // private static readonly ProtoId<TagPrototype> _theDarkTag = "TheDark"; // Wayfarer
    // private static readonly ProtoId<TagPrototype> _coreTag = "ShadekinCore"; // Wayfarer
    // private static readonly ProtoId<TagPrototype> _damagedCoreTag = "DamagedShadekinCore"; // Wayfarer
    // private TimeSpan _nextUpdate = TimeSpan.Zero; // Wayfarer
    // private TimeSpan _updateCooldown = TimeSpan.FromSeconds(1f); // Wayfarer

    private sealed class LightCone
    {
        public float Direction { get; set; }
        public float InnerWidth { get; set; }
        public float OuterWidth { get; set; }
    }
    private readonly Dictionary<string, List<LightCone>> lightMasks = new()
    {
        ["/Textures/Effects/LightMasks/cone.png"] = new List<LightCone>
    {
        new LightCone { Direction = 0, InnerWidth = 30, OuterWidth = 60 }
    },
        ["/Textures/Effects/LightMasks/double_cone.png"] = new List<LightCone>
    {
        new LightCone { Direction = 0, InnerWidth = 30, OuterWidth = 60 },
        new LightCone { Direction = 180, InnerWidth = 30, OuterWidth = 60 }
    },
        // Hardlight
        ["/Textures/_NF/Effects/LightMasks/beam.png"] = new List<LightCone>
    {
        new LightCone { Direction = 0, InnerWidth = 7.5f, OuterWidth = 15f }
    }
        // End Hardlight
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganShadekinCoreComponent, ExaminedEvent>(OnExamined);
        // SubscribeLocalEvent<OrganShadekinCoreComponent, OrganAddedToBodyEvent>(CoreOrganInit); // Wayfarer

        // SubscribeLocalEvent<ShadekinComponent, ComponentShutdown>((uid, _, _) => RemComp<BrighteyeComponent>(uid)); // Wayfarer
        // SubscribeLocalEvent<ShadekinComponent, EyeColorInitEvent>(OnEyeColorChange); // Wayfarer
        SubscribeLocalEvent<ShadekinComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        // SubscribeLocalEvent<ShadekinComponent, NullSpaceShuntEvent>(NullSpaceShunt);
        SubscribeLocalEvent<ShadekinComponent, BeforeDamageChangedEvent>((uid, _, args) => args.Damage.DamageDict["Asphyxiation"] = 0);

        // InitializeBrighteye(); // Wayfarer
        // InitializeAbilities(); // Wayfarer
    }

    private void CoreOrganInit(EntityUid uid, OrganShadekinCoreComponent component, OrganAddedToBodyEvent args)
        => component.OrganOwner ??= args.Body;

    private void OnExamined(EntityUid uid, OrganShadekinCoreComponent component, ref ExaminedEvent args)
    {
        if (!component.Damaged)
            args.PushMarkup(Loc.GetString("shadekin-core-undamaged"));

        if (component.OrganOwner == args.Examiner)
            args.PushMarkup(Loc.GetString("shadekin-core-owner"));
    }

    // Wayfarer
    // private void OnEyeColorChange(EntityUid uid, ShadekinComponent component, EyeColorInitEvent args)
    // {
    //     if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
    //         return;

    //     humanoid.EyeGlowing = false;
    //     Dirty(uid, humanoid);
    // }

    // private void NullSpaceShunt(EntityUid uid, ShadekinComponent component, NullSpaceShuntEvent args)
    // {
    //     if (TryComp<BodyComponent>(uid, out var body) && _bodySystem.TryGetOrgansWithComponent<OrganShadekinCoreComponent>((uid, body), out _)) // Wizden
    //     {
    //         _popup.PopupEntity(Loc.GetString("shadekin-shunt"), uid, uid, PopupType.LargeCaution);
    //         _stunSystem.TryKnockdown(uid, TimeSpan.FromSeconds(1), autoStand: false);
    //         ApplyCoreDamage(uid, 5);
    //     }
    // }
    // End Wayfarer

    public void UpdateAlert(EntityUid uid, ShadekinComponent component, short state)
        => _alerts.ShowAlert(uid, component.ShadekinAlert, state);

    private Angle GetAngle(EntityUid lightUid, SharedPointLightComponent lightComp, EntityUid targetUid)
    {
        var (lightPos, lightRot) = _transform.GetWorldPositionRotation(lightUid);
        lightPos += lightRot.RotateVec(lightComp.Offset);

        var (targetPos, targetRot) = _transform.GetWorldPositionRotation(targetUid);

        var mapDiff = targetPos - lightPos;

        var oppositeMapDiff = (-lightRot).RotateVec(mapDiff);
        var angle = oppositeMapDiff.ToWorldAngle();

        if (angle == double.NaN && _transform.ContainsEntity(targetUid, lightUid) || _transform.ContainsEntity(lightUid, targetUid))
        {
            angle = 0f;
        }

        return angle;
    }

    /// <summary>
    /// Return an illumination float value with is how many "energy" of light is hitting our ent.
    /// WARNING: This function might be expensive, Avoid calling it too much and CACHE THE RESULT!
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public float GetLightExposure(EntityUid uid)
    {
        var illumination = 0f;

        // Wayfarer
        // var shadeQuery = _lookup.GetEntitiesInRange<ShadegenComponent>(Transform(uid).Coordinates, 10); // Why 10 when theres different ranges? because light check does not go above 20.

        // foreach (var shadegen in shadeQuery)
        //     if (_transform.InRange(Transform(uid).Coordinates, Transform(shadegen.Owner).Coordinates, shadegen.Comp.Range))
        //         return illumination;
        // End Wayfarer

        var lightQuery = _lookup.GetEntitiesInRange<PointLightComponent>(Transform(uid).Coordinates, 10, LookupFlags.All | LookupFlags.Approximate);

        foreach (var light in lightQuery)
        {
            // Wayfarer
            // if (HasComp<DarkLightComponent>(light.Owner) || HasComp<ShadegenAffectedComponent>(light.Owner))
            //     continue;
            // End Wayfarer

            if (!light.Comp.Enabled
                || light.Comp.Radius < 1
                || light.Comp.Energy <= 0)
                continue;

            // Check if our entity is in a container with OccludesLight, if yes, is it the same as the light?
            if (_container.TryGetContainingContainer(uid, out var uidcontainer) && uidcontainer.OccludesLight && !_container.IsInSameOrNoContainer(uid, light.Owner))
                continue;

            // Same as above but this time we check the light entity instead of our entity.
            if (_container.TryGetContainingContainer(light.Owner, out var lightcontainer) && lightcontainer.OccludesLight && !_container.IsInSameOrNoContainer(uid, light.Owner))
                continue;

            if (!_examine.InRangeUnOccluded(light, uid, light.Comp.Radius, null))
                continue;

            Transform(uid).Coordinates.TryDistance(EntityManager, Transform(light).Coordinates, out var dist);

            var denom = dist / light.Comp.Radius;
            var attenuation = 1 - (denom * denom);
            var calculatedLight = 0f;

            if (light.Comp.MaskPath is not null)
            {
                var angleToTarget = GetAngle(light, light.Comp, uid);
                foreach (var cone in lightMasks[light.Comp.MaskPath])
                {
                    var coneLight = 0f;
                    var angleAttenuation = (float)Math.Min((float)Math.Max(cone.OuterWidth - angleToTarget, 0f), cone.InnerWidth) / cone.OuterWidth;

                    if (angleToTarget.Degrees - cone.Direction > cone.OuterWidth)
                        continue;
                    else if (angleToTarget.Degrees - cone.Direction > cone.InnerWidth
                        && angleToTarget.Degrees - cone.Direction < cone.OuterWidth)
                        coneLight = light.Comp.Energy * attenuation * attenuation * angleAttenuation;
                    else
                        coneLight = light.Comp.Energy * attenuation * attenuation;

                    calculatedLight = Math.Max(calculatedLight, coneLight);
                }
            }
            else
                calculatedLight = light.Comp.Energy * attenuation * attenuation;

            illumination += calculatedLight; //Math.Max(illumination, calculatedLight);
        }

        return illumination;
    }

    private void SetPassiveBuff(EntityUid uid, ShadekinState shadekinState)
    {
        if (!TryComp<PassiveDamageComponent>(uid, out var passive))
            return;

        if (shadekinState is ShadekinState.Annoying or
            ShadekinState.High or
            ShadekinState.Extreme)
        {
            passive.DamageCap = 1;
        }
        else if (shadekinState == ShadekinState.Low)
        {
            passive.DamageCap = 20;
            passive.AllowedStates.Clear();
            passive.AllowedStates.Add(MobState.Alive);
            passive.Interval = 1f;
        }
        else if (shadekinState == ShadekinState.Dark)
        {
            passive.DamageCap = 0;
            passive.AllowedStates.Clear();
            passive.AllowedStates.Add(MobState.Alive);
            passive.AllowedStates.Add(MobState.Critical);
            passive.AllowedStates.Add(MobState.Dead);
            passive.Interval = 0.5f;
        }
    }

    private void ApplyLightDamage(EntityUid uid, float dmg)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Heat", dmg);
        _damageable.TryChangeDamage(uid, damage, true, false);
    }

    // Wayfarer
    // private void ApplyCoreDamage(EntityUid uid, float dmg)
    // {
    //     var damage = new DamageSpecifier();
    //     damage.DamageDict.Add("Cellular", dmg);
    //     _damageable.TryChangeDamage(uid, damage, false, false);
    // }
    // End Wayfarer

    private void OnRefreshMovementSpeedModifiers(EntityUid uid, ShadekinComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.CurrentState is ShadekinState.High or ShadekinState.Extreme)
        {
            if (!TryComp<MovementSpeedModifierComponent>(uid, out var movement))
                return;

            var sprintDif = movement.BaseWalkSpeed / movement.BaseSprintSpeed;
            args.ModifySpeed(1f, sprintDif);
        }
    }

    // Wayfarer: No NightVision
    // private void ToggleNightVision(EntityUid uid, ShadekinState shadekinState)
    // {
    //     var nightVision = EnsureComp<NightVisionComponent>(uid);
    //     var shouldBeActive = shadekinState == ShadekinState.Dark;

    //     // avoid dirtying if we don't need to
    //     if (nightVision.Active == shouldBeActive)
    //         return;

    //     // update whether or not nightVision should be active based on light level
    //     nightVision.Active = shouldBeActive;

    //     // ensure nightVision updates to reflect the new state
    //     Dirty(uid, nightVision);
    // }
    // End Wayfarer

    private void CheckThresholds(EntityUid uid, ShadekinComponent component, float lightExposure)
    {
        foreach (var (threshold, shadekinState) in component.Thresholds.Reverse())
        {
            var selectedstate = shadekinState;
            if (lightExposure < threshold)
            {
                if (selectedstate == ShadekinState.Low) // If Low is below the threshold, then we auto-jump to Dark.
                    selectedstate = ShadekinState.Dark;
                else
                    continue;
            }

            component.CurrentState = selectedstate;
            UpdateAlert(uid, component, (short)selectedstate);
            Dirty(uid, component);
            break;
        }
    }

    // Wayfarer
    // /// <summary>
    // /// Makes a simple check to see if the ent is in the dark.
    // /// </summary>
    // /// <param name="uid"></param>
    // /// <returns></returns>
    // public bool AreWeInTheDark(EntityUid uid)
    // {
    //     var mapUid = Transform(uid).MapUid;
    //     if (mapUid is not null && _tag.HasTag(mapUid.Value, _theDarkTag))
    //         return true;

    //     return false;
    // }
    // End Wayfarer

    // Wayfarer
    // /// <summary>
    // /// Spawn "The Dark"
    // /// </summary>
    // public void SpawnTheDark()
    // {
    //     var query = EntityQueryEnumerator<MapComponent>();
    //     while (query.MoveNext(out var mapuid, out var mapcomp))
    //     {
    //         if (mapcomp.MapPaused)
    //             continue;

    //         if (_tag.HasTag(mapuid, _theDarkTag))
    //             return;
    //     }
    //     _gameTicker.StartGameRule("TheDarkMap");
    // }
    // End Wayfarer

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShadekinComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.NextUpdate)
                continue;

            component.NextUpdate = _timing.CurTime + component.UpdateCooldown;

            // var lightExposure = 0f; // Wayfarer

            // Wayfarer
            // if (HasComp<NullSpaceComponent>(uid) || AreWeInTheDark(uid)) // Were in NullSpace, NullSpace is dark... and "The Dark" is dark too!
            // {
            //     // I had a brain moment, apprently if one is false its does not check for the other?
            // }
            // else
            var lightExposure = GetLightExposure(uid);
            // End Wayfarer


            CheckThresholds(uid, component, lightExposure);

            // ToggleNightVision(uid, component.CurrentState); // Wayfarer: No Nightvision
            SetPassiveBuff(uid, component.CurrentState);
            _speed.RefreshMovementSpeedModifiers(uid);

            if (component.CurrentState == ShadekinState.Extreme)
                ApplyLightDamage(uid, 1);

            // Wayfarer
            // if (TryComp<BrighteyeComponent>(uid, out var brighteye))
            //     UpdateEnergy(uid, component, brighteye);
            // End Wayfarer
        }

        // Wayfarer
        // The Dark Effects - This only applies for Ents that are IN THE DARK.
        //     if (_timing.CurTime > _nextUpdate)
        //     {
        //         _nextUpdate = _timing.CurTime + _updateCooldown;

        //         var thedarkmobquery = EntityQueryEnumerator<MobStateComponent>();
        //         while (thedarkmobquery.MoveNext(out var uid, out var _))
        //         {
        //             var remove = false;

        //             if (_status.HasStatusEffect(uid, "StatusEffectTheDarkMap"))
        //             {
        //                 if (HasComp<ShadekinComponent>(uid) || HasComp<TheDarkImmuneComponent>(uid))
        //                     remove = true;

        //                 if (!remove)
        //                     foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
        //                         if (TryComp<TheDarkImmuneComponent>(entity, out var blocker) && blocker.Ranged)
        //                             remove = true;
        //             }

        //             if (AreWeInTheDark(uid) && !remove)
        //                 _status.TrySetStatusEffectDuration(uid, "StatusEffectTheDarkMap");
        //             else
        //                 _status.TryRemoveStatusEffect(uid, "StatusEffectTheDarkMap");
        //         }
        //     }
        // End Wayfarer
    }
}
