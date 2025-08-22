// New Frontiers - This file is licensed under AGPLv3
// Copyright (c) 2024 New Frontiers Contributors
// See AGPLv3.txt for details.
using Content.Server._NF.Station.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Audio;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    [Dependency] private readonly RadarConsoleSystem _radarConsole = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly PopupSystem _popupSystem = null!;
    public TimeSpan BrakeDelay = TimeSpan.FromSeconds(10);
    public TimeSpan NextBrakeCheck = TimeSpan.Zero;

    private const float SpaceFrictionStrength = 0.0075f;
    private const float DampenDampingStrength = 0.25f;
    private const float AnchorDampingStrength = 2.5f;
    private void NfInitialize()
    {
        SubscribeLocalEvent<ShuttleConsoleComponent, SetInertiaDampeningRequest>(OnSetInertiaDampening);
        SubscribeLocalEvent<ShuttleConsoleComponent, SetServiceFlagsRequest>(NfSetServiceFlags);
        SubscribeLocalEvent<ShuttleConsoleComponent, SetTargetCoordinatesRequest>(NfSetTargetCoordinates);
        SubscribeLocalEvent<ShuttleConsoleComponent, SetHideTargetRequest>(NfSetHideTarget);
    }

    private bool SetInertiaDampening(EntityUid uid, PhysicsComponent physicsComponent, ShuttleComponent shuttleComponent, TransformComponent transform, InertiaDampeningMode mode)
    {
        if (!transform.GridUid.HasValue)
        {
            return false;
        }

        if (mode == InertiaDampeningMode.Query)
        {
            _console.RefreshShuttleConsoles(transform.GridUid.Value);
            return false;
        }

        if (!EntityManager.HasComponent<ShuttleDeedComponent>(transform.GridUid) ||
            EntityManager.HasComponent<StationDampeningComponent>(_station.GetOwningStation(transform.GridUid)))
        {
            return false;
        }

        shuttleComponent.BodyModifier = mode switch
        {
            InertiaDampeningMode.Off => SpaceFrictionStrength,
            InertiaDampeningMode.Dampen => DampenDampingStrength,
            InertiaDampeningMode.Anchor => AnchorDampingStrength,
            _ => DampenDampingStrength, // other values: default to some sane behaviour (assume normal dampening)
        };

        if (shuttleComponent.DampingModifier != 0)
            shuttleComponent.DampingModifier = shuttleComponent.BodyModifier;
        shuttleComponent.EBrakeActive = false;
        _console.RefreshShuttleConsoles(transform.GridUid.Value);
        return true;
    }

    private void OnSetInertiaDampening(EntityUid uid, ShuttleConsoleComponent component, SetInertiaDampeningRequest args)
    {
        // Ensure that the entity requested is a valid shuttle (stations should not be togglable)
        if (!EntityManager.TryGetComponent(uid, out TransformComponent? transform) ||
            !transform.GridUid.HasValue ||
            !EntityManager.TryGetComponent(transform.GridUid, out PhysicsComponent? physicsComponent) ||
            !EntityManager.TryGetComponent(transform.GridUid, out ShuttleComponent? shuttleComponent))
        {
            return;
        }

        if (SetInertiaDampening(uid, physicsComponent, shuttleComponent, transform, args.Mode) && args.Mode != InertiaDampeningMode.Query)
            component.DampeningMode = args.Mode;
    }

    public InertiaDampeningMode NfGetInertiaDampeningMode(EntityUid entity)
    {
        if (!EntityManager.TryGetComponent<TransformComponent>(entity, out var xform))
            return InertiaDampeningMode.Dampen;

        // Not a shuttle, shouldn't be togglable
        if (!EntityManager.HasComponent<ShuttleDeedComponent>(xform.GridUid) ||
            EntityManager.HasComponent<StationDampeningComponent>(_station.GetOwningStation(xform.GridUid)))
            return InertiaDampeningMode.Station;

        if (!EntityManager.TryGetComponent(xform.GridUid, out ShuttleComponent? shuttle))
            return InertiaDampeningMode.Dampen;

        if (shuttle.EBrakeActive)
            return InertiaDampeningMode.Emergency; // mainly to uncheck the thing in the UI

        if (shuttle.BodyModifier >= AnchorDampingStrength)
            return InertiaDampeningMode.Anchor;
        else if (shuttle.BodyModifier <= SpaceFrictionStrength)
            return InertiaDampeningMode.Off;
        else
            return InertiaDampeningMode.Dampen;
    }

    public void NfSetPowered(EntityUid uid, ShuttleConsoleComponent component, bool powered)
    {
        // Ensure that the entity requested is a valid shuttle (stations should not be togglable)
        if (!EntityManager.TryGetComponent(uid, out TransformComponent? transform) ||
            !transform.GridUid.HasValue ||
            !EntityManager.TryGetComponent(transform.GridUid, out PhysicsComponent? physicsComponent) ||
            !EntityManager.TryGetComponent(transform.GridUid, out ShuttleComponent? shuttleComponent))
        {
            return;
        }

        // Update dampening physics without adjusting requested mode.
        if (!powered)
        {
            SetInertiaDampening(uid, physicsComponent, shuttleComponent, transform, InertiaDampeningMode.Anchor);
        }
        else
        {
            // Update our dampening mode if we need to, and if we aren't a station.
            var currentDampening = NfGetInertiaDampeningMode(uid);
            if (currentDampening != component.DampeningMode &&
                currentDampening != InertiaDampeningMode.Station &&
                component.DampeningMode != InertiaDampeningMode.Station)
            {
                SetInertiaDampening(uid, physicsComponent, shuttleComponent, transform, component.DampeningMode);
            }
        }
    }

    /// <summary>
    /// Get the current service flags for this grid.
    /// </summary>
    public ServiceFlags NfGetServiceFlags(EntityUid uid)
    {
        var transform = Transform(uid);
        // Get the grid entity from the console transform
        if (!transform.GridUid.HasValue)
            return ServiceFlags.None;

        var gridUid = transform.GridUid.Value;

        // Set the service flags on the IFFComponent.
        if (!EntityManager.TryGetComponent<IFFComponent>(gridUid, out var iffComponent))
            return ServiceFlags.None;

        return iffComponent.ServiceFlags;
    }

    /// <summary>
    /// Set the service flags for this grid.
    /// </summary>
    public void NfSetServiceFlags(EntityUid uid, ShuttleConsoleComponent component, SetServiceFlagsRequest args)
    {
        var transform = Transform(uid);
        // Get the grid entity from the console transform
        if (!transform.GridUid.HasValue)
            return;

        var gridUid = transform.GridUid.Value;

        // Set the service flags on the IFFComponent.
        if (!EntityManager.TryGetComponent<IFFComponent>(gridUid, out var iffComponent))
            return;

        iffComponent.ServiceFlags = args.ServiceFlags;
        _console.RefreshShuttleConsoles(gridUid);
        Dirty(gridUid, iffComponent);
    }

    public void NfSetTargetCoordinates(EntityUid uid, ShuttleConsoleComponent component, SetTargetCoordinatesRequest args)
    {
        if (!TryComp<RadarConsoleComponent>(uid, out var radarConsole))
            return;

        var transform = Transform(uid);
        // Get the grid entity from the console transform
        if (!transform.GridUid.HasValue)
            return;

        var gridUid = transform.GridUid.Value;

        _radarConsole.SetTarget((uid, radarConsole), args.TrackedEntity, args.TrackedPosition);
        _radarConsole.SetHideTarget((uid, radarConsole), false); // Force target visibility
        _console.RefreshShuttleConsoles(gridUid);
    }

    public void NfSetHideTarget(EntityUid uid, ShuttleConsoleComponent component, SetHideTargetRequest args)
    {
        if (!TryComp<RadarConsoleComponent>(uid, out var radarConsole))
            return;

        var transform = Transform(uid);
        // Get the grid entity from the console transform
        if (!transform.GridUid.HasValue)
            return;

        var gridUid = transform.GridUid.Value;

        _radarConsole.SetHideTarget((uid, radarConsole), args.Hidden);
        _console.RefreshShuttleConsoles(gridUid);
    }

    /// <summary>
    /// Throws on the emergency brake for any shuttle that:
    /// Is a player shuttle, AND
    /// Doesn't have anyone in it OR
    /// everyone inside is either in crit or dead OR
    /// The shuttle console is not powered or EMPed
    /// </summary>
    public void ShouldEmergencyBrake()
    {
        var curTime = _gameTiming.CurTime;
        if (curTime < NextBrakeCheck)
            return;
        NextBrakeCheck = curTime + BrakeDelay;
        var query = EntityQueryEnumerator<ShuttleComponent>();
        var whereIsEveryone = GetPlayerShipsWithPeopleOnThem();

        while (query.MoveNext(out var uid, out var shuttle))
        {
            if (shuttle.EBrakeActive)
            {
                continue;
            }
            if (!shuttle.PlayerShuttle)
            {
                continue;
            }
            if (shuttle.DampingModifier > SpaceFrictionStrength)
            {
                // Its already able to slow down on its own, no need to emergency brake
                continue;
            }
            // If the shuttle is not moving, no need to emergency brake
            if (!TryComp(uid, out PhysicsComponent? gridBody))
            {
                Log.Warning($"Shuttle {ToPrettyString(uid)} does not have a PhysicsComponent!!!");
                continue;
            }
            // if the shuttle is moving under a certain speed, just quietly engage the emergency brake
            var quietly = false;
            var gridVelocity = gridBody.LinearVelocity;
            if (gridVelocity.LengthSquared() < 1f)
            {
                continue; // no need to emergency brake, shuttle is basically stationary
            }
            if (gridVelocity.LengthSquared() < 25f) // 5 squared
            {
                quietly = true; // shuttle is slowly moving, engage the emergency brake quietly
            }

            var mygrid = Transform(uid).GridUid;
            if (mygrid is null)
            {
                continue;
            }
            HashSet<Entity<ShuttleConsoleComponent>> cronsoles = new();
            _lookup.GetChildEntities(mygrid ?? uid, cronsoles);
            // is the shuttle present in the list of player ships with people on them?
            if (!whereIsEveryone.Contains(shuttle))
            {
                EngageEmergencyBrake(
                    uid,
                    shuttle,
                    cronsoles,
                    quietly); // no need to emergency brake, people are on it
                continue;
            }
            // find all the shuttle consoles on this shuttle
            if (cronsoles.Count == 0)
            {
                EngageEmergencyBrake(
                    uid,
                    shuttle,
                    cronsoles,
                    quietly); // no powered consoles, emergency brake
                continue;
            }
            // check if any of the shuttle consoles are powered
            var poweredFound = false;
            foreach (var console in cronsoles)
            {
                var consoleEntity = console.Owner;
                if (!this.IsPowered(consoleEntity, EntityManager))
                    continue;
                poweredFound = true;
                break; // at least one console is powered, no need to emergency brake
            }
            if (!poweredFound)
            {
                EngageEmergencyBrake(
                    uid,
                    shuttle,
                    cronsoles,
                    quietly); // no powered consoles, emergency brake
                continue;
            }
        }
    }

    /// <summary>
    /// Returns a dictionary of:
    /// Shuttle UIDs where: it is a player shuttle, and players are inside, and at least one player is alive.
    /// </summary>
    private List<ShuttleComponent> GetPlayerShipsWithPeopleOnThem()
    {
        var whereDict = new List<ShuttleComponent>(); // awoo
        foreach (var sesh in _players.Sessions)
        {
            // Get the player entity
            if (!sesh.AttachedEntity.HasValue)
            {
                Log.Debug($"Skipping for E-Brake: Player session {sesh.Name} ({sesh.UserId}) has no attached entity.");
                continue;
            }
            var attached = sesh.AttachedEntity.Value;
            // If the player is in crit or dead, skip them
            if (!_mobState.IsAlive(attached)
                || HasComp<GhostComponent>(attached))
            {
                Log.Debug($"Skipping for E-Brake: Player session {sesh.Name} ({sesh.UserId}) is in crit or dead.");
                continue;
            }

            // Get the shuttle the player is on, if any
            if (!EntityManager.TryGetComponent(attached, out TransformComponent? transform)
                || !transform.GridUid.HasValue
                || !TryComp<ShuttleComponent>(transform.GridUid.Value, out var shuttle)
                || !shuttle.PlayerShuttle)
            {
                Log.Debug($"Skipping for E-Brake: Player session {sesh.Name} ({sesh.UserId}) is not on a player shuttle.");
                continue;
            }
            whereDict.Add(shuttle);
        }
        return whereDict;
    }

    /// <summary>
    /// Turns on the emergency brake for a given shuttle.
    /// </summary>
    private void EngageEmergencyBrake(
        EntityUid uid,
        ShuttleComponent shuttle,
        HashSet<Entity<ShuttleConsoleComponent>> consoles,
        bool quietly = false)
    {
        if (shuttle.EBrakeActive)
        {
            return;
        }

        if (!EntityManager.TryGetComponent(uid, out TransformComponent? transform)
            || !transform.GridUid.HasValue
            || !EntityManager.TryGetComponent(transform.GridUid, out PhysicsComponent? physicsComponent))
        {
            return;
        }
        Log.Debug($"Engaging E-Brake for {ToPrettyString(uid)}.");
        SetInertiaDampening(
            uid,
            physicsComponent,
            shuttle,
            transform,
            InertiaDampeningMode.Anchor);
        shuttle.EBrakeActive = true;
        if (consoles.Count > 0)
        {
            SoundSpecifier eBrakeBeep = quietly switch
            {
                true => new SoundPathSpecifier("/Audio/_COYOTE/ShuttleStuff/ShuttleEBrakeEngagedQuietly.ogg"),
                false => new SoundPathSpecifier("/Audio/_COYOTE/ShuttleStuff/ShuttleEBrakeEngaged.ogg"),
            };
            var audioParams = quietly switch
            {
                true => AudioParams.Default.WithVariation(SharedContentAudioSystem.DefaultVariation).WithVolume(1f).WithMaxDistance(10f),
                false => AudioParams.Default.WithVariation(SharedContentAudioSystem.DefaultVariation).WithVolume(3f).WithMaxDistance(20f),
            };

            foreach (var console in consoles)
            {
                // get the entity the console is attached to
                var consoleEntity = console.Owner;
                _audio.PlayPvs(
                    eBrakeBeep,
                    consoleEntity,
                    audioParams);
                if (!quietly) // throw in a BANG to make it more dramatic
                {
                    _audio.PlayPvs(
                        _shuttleImpactSound,
                        consoleEntity,
                        audioParams.WithVolume(5f));
                }
                if (quietly)
                {
                    _popupSystem.PopupEntity(
                        "Emergency Brake Engaged",
                        consoleEntity,
                        PopupType.MediumCaution);
                }
                else
                {
                    _popupSystem.PopupEntity(
                        "EMERGENCY BRAKE ENGAGED!!",
                        consoleEntity,
                        PopupType.LargeCaution);
                }
            }
        }
    }




}
