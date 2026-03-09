// Wayfarer: Modified to support multiple stored characters - commented out PlayerBeforeSpawnEvent reset
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Shared.Bed.Sleep;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared._NF.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Players;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Content.Shared._NF.CryoSleep.Events;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Ghost;

namespace Content.Server._NF.CryoSleep;

public sealed partial class CryoSleepSystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    private void InitReturning()
    {
        SubscribeNetworkEvent<WakeupRequestMessage>(OnWakeupMessage);
        // Don't reset on lobby join - allow players to resume their cryo characters
        // SubscribeLocalEvent<PlayerJoinedLobbyEvent>(e => ResetCryosleepState(e.PlayerSession.UserId));
        // Don't reset on spawn - allow players to have multiple characters in cryo
        // SubscribeLocalEvent<PlayerBeforeSpawnEvent>(e => ResetCryosleepState(e.Player.UserId));
    }

    private void OnWakeupMessage(WakeupRequestMessage message, EntitySessionEventArgs session)
    {
        var entity = session.SenderSession.GetMind();

        var result = entity == null || !TryComp<MindComponent>(entity, out var mind)
            ? ReturnToBodyStatus.NotAGhost
            : TryReturnToBody(mind);

        var msg = new WakeupRequestMessage.Response(result);
        RaiseNetworkEvent(msg, session.SenderSession);
    }

    /// <summary>
    ///   Returns the mind to the original body, if any. The mind must be possessing a ghost, unless [force] is true.
    /// </summary>
    public ReturnToBodyStatus TryReturnToBody(MindComponent mind, bool force = false)
    {
        if (!_configurationManager.GetCVar(NFCCVars.CryoReturnEnabled))
            return ReturnToBodyStatus.Disabled;

        var id = mind.UserId;
        if (id == null || !_storedBodies.TryGetValue(id.Value, out var storedBodies) || storedBodies.Count == 0)
            return ReturnToBodyStatus.BodyMissing;

        if (!force && (mind.CurrentEntity is not { Valid: true } ghost || !HasComp<GhostComponent>(ghost)))
            return ReturnToBodyStatus.NotAGhost;

        // Use the first stored body
        var storedBody = storedBodies[0];
        var cryopod = storedBody.Cryopod;
        var body = storedBody.Body;
        
        if (!Exists(cryopod) || Deleted(cryopod) || !TryComp<CryoSleepComponent>(cryopod, out var cryoComp))
        {
            var fallbackQuery = EntityQueryEnumerator<CryoSleepFallbackComponent, CryoSleepComponent>();
            bool foundFallback = false;
            while (fallbackQuery.MoveNext(out cryopod, out _, out cryoComp))
            {
                if (!IsOccupied(cryoComp) && _container.Insert(body, cryoComp.BodyContainer))
                {
                    foundFallback = true;
                    break;
                }
            }

            // No valid cryopod, all fallbacks occupied or missing.
            if (!foundFallback)
                return ReturnToBodyStatus.NoCryopodAvailable;
        }
        else
        {
            // NOTE: if the pod is occupied but still exists, do not let the user teleport.
            if (IsOccupied(cryoComp!) || !_container.Insert(body, cryoComp!.BodyContainer))
                return ReturnToBodyStatus.Occupied;
        }

        _storedBodies.Remove(id.Value);
        _mind.ControlMob(id.Value, body);
        // Force the mob to sleep
        var sleep = EnsureComp<SleepingComponent>(body);
        sleep.CooldownEnd = TimeSpan.FromSeconds(5);

        _popup.PopupEntity(Loc.GetString("cryopod-wake-up", ("entity", body)), body);

        RaiseLocalEvent(body, new CryosleepWakeUpEvent(cryopod, id), true);

        _adminLogger.Add(LogType.LateJoin, LogImpact.Medium, $"{id.Value} has returned from cryosleep!");
        return ReturnToBodyStatus.Success;
    }

    /// <summary>
    ///   Removes the body of the given user from the cryosleep dictionary, making them unable to return to it.
    ///   Also actually deletes the body if it's still on that map.
    /// </summary>
    public void ResetCryosleepState(NetUserId id)
    {
        if (!_storedBodies.TryGetValue(id, out var bodies))
            return;

        _storedBodies.Remove(id);

        // If the user's a ghost, let them know their body's been removed.
        if (_mind.TryGetMind(id, out _, out var mindComp)
            && TryComp<GhostComponent>(mindComp.CurrentEntity, out var ghost))
        {
            _ghost.SetCanReturnFromCryo(ghost, false);
        }

        // Delete all stored bodies if they're still on the storage map
        foreach (var body in bodies)
        {
            if (Transform(body.Body).MapUid == _storageMap)
            {
                QueueDel(body.Body);
            }
        }
    }

    public bool HasCryosleepingBody(NetUserId id)
    {
        return _storedBodies.ContainsKey(id);
    }

    public bool TryGetSleepingBody(NetUserId userId, [NotNullWhen(true)] out EntityUid? body, [NotNullWhen(true)] out EntityUid? pod)
    {
        if (_storedBodies.TryGetValue(userId, out var storedBodies) && storedBodies.Count > 0)
        {
            var storedBody = storedBodies[0];
            body = storedBody.Body;
            pod = storedBody.Cryopod;
            return true;
        }
        else
        {
            body = null;
            pod = null;
            return false;
        }
    }
}
