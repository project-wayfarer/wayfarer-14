using System.Numerics;
using Content.Shared._Goobstation.Vehicles;
using Content.Shared._NF.Radar;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._NF.Radar;

/// <summary>
/// A system that handles and rate-limits client-made requests for radar blips.
/// </summary>
/// <remarks>
/// Ported from Monolith's RadarBlipsSystem.
/// </remarks>
public sealed partial class RadarBlipSystem : SharedRadarBlipSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private Dictionary<NetUserId, TimeSpan> _nextBlipRequestPerUser = new();

    // Wayfarer: rate-limit "blips dirty" pushes so a burst of new projectiles (e.g. grapeshot)
    // doesn't flood the network. Clients still won't request more often than their own throttle.
    private TimeSpan _nextDirtyPush = TimeSpan.Zero;
    private static readonly TimeSpan DirtyPushInterval = TimeSpan.FromMilliseconds(100);

    // The minimum amount of time between handled blip requests.
    private static readonly TimeSpan MinRequestPeriod = TimeSpan.FromMilliseconds(850); // Wayfarer: TimeSpan.FromSeconds(1)<TimeSpan.FromMilliseconds(250) Faster update for tracking shipgun projectiles.
    // Maximum distance for blips to be considered visible
    private const float MaxBlipRenderDistance = 300f;
    // Blink interval for critical state (in seconds)
    private const double CritBlinkInterval = 0.5;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        // Wayfarer: When a new radar blip enters the world (e.g. a fired projectile), tell
        // active clients to immediately re-request blips instead of waiting on their throttle.
        SubscribeLocalEvent<RadarBlipComponent, ComponentStartup>(OnBlipStartup);
    }

    /// <summary>
    /// Handles a network request for radar blips and sends the blip data to the requesting client.
    /// </summary>
    private void OnBlipsRequested(RequestBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(ev.Radar, out var radarUid))
            return;

        if (!TryComp<RadarConsoleComponent>(radarUid, out var radar))
            return;

        if (_nextBlipRequestPerUser.TryGetValue(args.SenderSession.UserId, out var requestTime) && _timing.RealTime < requestTime)
            return;

        _nextBlipRequestPerUser[args.SenderSession.UserId] = _timing.RealTime + MinRequestPeriod;

        var blips = AssembleBlipsReport((radarUid.Value, radar));

        var giveEv = new GiveBlipsEvent(blips);
        RaiseNetworkEvent(giveEv, args.SenderSession);
    }

    /// <summary>
    /// Clears blip request data between rounds.
    /// </summary>
    public void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _nextBlipRequestPerUser.Clear();
        _nextDirtyPush = TimeSpan.Zero;
    }

    /// <summary>
    /// Wayfarer: notify clients that the blip set has changed so they can fetch the new blip
    /// without waiting for their normal poll interval. Also clears server-side per-user rate
    /// limits so the resulting request is honored immediately.
    /// </summary>
    private void OnBlipStartup(EntityUid uid, RadarBlipComponent component, ComponentStartup args)
    {
        if (_timing.RealTime < _nextDirtyPush)
            return;

        _nextDirtyPush = _timing.RealTime + DirtyPushInterval;
        _nextBlipRequestPerUser.Clear();
        RaiseNetworkEvent(new RadarBlipsDirtyEvent());
    }

    /// <summary>
    /// Assembles a list of radar blips visible to the given radar console.
    /// </summary>
    private List<(NetEntity? Grid, Vector2 Position, Vector2 Velocity, float Scale, Color Color, RadarBlipShape Shape)> AssembleBlipsReport(Entity<RadarConsoleComponent> ent)
    {
        var blips = new List<(
            NetEntity? Grid,
            Vector2 Position,
            Vector2 Velocity,
            float Scale,
            Color Color,
            RadarBlipShape Shape)>();

        if (!TryComp(ent, out TransformComponent? radarXform))
            return blips;
        var radarPosition = _xform.GetWorldPosition(ent);
        var radarGrid = radarXform.GridUid;
        var radarMapId = radarXform.MapID;
        var radarRange = MathF.Min(ent.Comp.MaxRange, MaxBlipRenderDistance);

        // Non-positive range, nothing to return.
        if (radarRange <= 0)
            return blips;

        var blipQuery = EntityQueryEnumerator<RadarBlipComponent, TransformComponent>();

        while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform))
        {
            if (!blip.Enabled)
            {
                // Log.Debug($"Blip {blipUid} skipped: not enabled.");
                continue;
            }

            // Wayfarer: If this blip's parent is itself a blip, the parent already represents it on radar.
            // Skip to avoid overlapping blips.
            if (blipXform.ParentUid.IsValid() && HasComp<RadarBlipComponent>(blipXform.ParentUid))
            {
                // Log.Debug($"Blip {blipUid} skipped: parent already has a blip.");
                continue;
            }

            if (blipXform.MapID != radarMapId)
            {
                // Log.Debug($"Blip {blipUid} skipped: different map.");
                continue;
            }

            // Run cheaper grid checks before distance checks
            var blipGrid = blipXform.GridUid;
            if (blip.RequireNoGrid && blipGrid != null)
            {
                Log.Debug($"Blip {blipUid} skipped: has grid but requires none.");
                continue;
            }

            if (!blip.VisibleFromOtherGrids && blipGrid != radarGrid)
            {
                Log.Debug($"Blip {blipUid} skipped: not on same grid as radar.");
                continue;
            }

            var blipPosition = _xform.GetWorldPosition(blipUid);
            var distance = (blipPosition - radarPosition).Length();
            if (distance > radarRange)
            {
                Log.Debug($"Blip {blipUid} skipped: out of range.");
                continue;
            }

            // Wayfarer: capture linear velocity for client-side prediction so fast-moving
            // entities (e.g. shipgun cannonballs) get smooth blip movement between the
            // relatively-slow server blip updates.
            var blipVelocity = Vector2.Zero;
            if (TryComp<PhysicsComponent>(blipUid, out var blipPhysics))
                blipVelocity = blipPhysics.LinearVelocity;

            // Convert blip position to grid coords if needed.
            NetEntity? blipNetGrid = null;
            if (blipGrid != null)
            {
                blipNetGrid = GetNetEntity(blipGrid.Value);
                blipPosition = Vector2.Transform(blipPosition, _xform.GetInvWorldMatrix(blipGrid.Value));
                // Rotate velocity into the grid's local frame (translation does not affect a velocity vector).
                var gridInvRot = -_xform.GetWorldRotation(blipGrid.Value);
                blipVelocity = gridInvRot.RotateVec(blipVelocity);
            }
            var scale = blip.Scale;
            var shape = blip.Shape;
            var color = blip.RadarColor;

            // Check if entity or its parent is in critical state and modify color
            var entityToCheck = blipUid;

            // If this blip doesn't have a mob state, check the parent (e.g., player holding a PDA)
            if (!HasComp<MobStateComponent>(entityToCheck) && blipXform.ParentUid.IsValid())
            {
                entityToCheck = blipXform.ParentUid;
            }

            if (TryComp<MobStateComponent>(entityToCheck, out var mobState))
            {
                if (_mobState.IsCritical(entityToCheck, mobState))
                {
                    // Blink between red and original color
                    var blinkPhase = (_timing.RealTime.TotalSeconds % CritBlinkInterval) / CritBlinkInterval;
                    color = blinkPhase < 0.5 ? Color.Red : color;
                }
            }

            // {
            //     var ev = new RadarBlipEvent(
            //         color,
            //         shape,
            //         scale,
            //         blip.Enabled);
            //     RaiseLocalEvent(blipUid, ref ev);
            //     scale = ev.ChangeScale ?? scale;
            //     color = ev.ChangeColor ?? color;
            //     shape = ev.ChangeShape ?? shape;
            //     if (ev.ChangeEnabled.HasValue)
            //     {
            //         blip.Enabled = ev.ChangeEnabled.Value;
            //         if (!blip.Enabled)
            //         {
            //             Log.Debug($"Blip {blipUid} skipped: disabled by event.");
            //             continue;
            //         }
            //     }
            // }

            blips.Add((blipNetGrid, blipPosition, blipVelocity, scale, color, shape));
        }
        return blips;
    }

    /// <summary>
    /// Configures the radar blip for a jetpack or vehicle entity.
    /// </summary>
    private void SetupRadarBlip(EntityUid uid, Color color, float scale, bool visibleFromOtherGrids = true, bool requireNoGrid = false)
    {
        var blip = EnsureComp<RadarBlipComponent>(uid);
        blip.RadarColor = color;
        blip.Scale = scale;
        blip.VisibleFromOtherGrids = visibleFromOtherGrids;
        blip.RequireNoGrid = requireNoGrid;
    }

    /// <summary>
    /// Configures the radar blip for a vehicle entity.
    /// </summary>
    public void SetupVehicleRadarBlip(Entity<VehicleComponent> uid)
    {
        SetupRadarBlip(uid, Color.Cyan, 1f, true, true);
    }
}
