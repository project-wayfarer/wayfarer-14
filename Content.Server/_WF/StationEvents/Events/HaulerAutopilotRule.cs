using System.Numerics;
using Content.Server._WF.StationEvents.Components;
using Content.Server._WF.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._WF.StationEvents.Events;

/// <summary>
/// Event system that spawns a hauler shuttle at a distance and engages its autopilot
/// to navigate to the opposite side of the map.
/// </summary>
public sealed class HaulerAutopilotRuleSystem : StationEventSystem<HaulerAutopilotRuleComponent>
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly AutopilotSystem _autopilot = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly MapSystem _map = default!;

    protected override void Started(EntityUid uid, HaulerAutopilotRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Get the main game map
        if (!_map.TryGetMap(GameTicker.DefaultMap, out var mapUid))
            return;

        // Create a temporary map to load the shuttle on
        _map.CreateMap(out var tempMapId);

        // Load the shuttle
        if (!_loader.TryLoadGrid(tempMapId, component.ShuttlePath, out var shuttleGrid))
        {
            Log.Error($"Failed to load hauler shuttle from {component.ShuttlePath}");
            _mapManager.DeleteMap(tempMapId);
            return;
        }

        // Get the grid entity
        var shuttleUid = shuttleGrid.Value;
        component.ShuttleUid = shuttleUid;

        // Generate a random spawn position at the specified distance
        var spawnAngle = _random.NextAngle();
        var spawnDistance = _random.NextFloat(component.MinimumDistance, component.MaximumDistance);
        var spawnPosition = new Vector2(
            MathF.Cos((float)spawnAngle.Theta) * spawnDistance,
            MathF.Sin((float)spawnAngle.Theta) * spawnDistance
        );

        // Generate the target position on the opposite side with an angular offset
        // to ensure it doesn't pass through the center (stays at least 2000 units away)
        // Add a random angular offset between 45 and 135 degrees (Pi/4 to 3*Pi/4)
        var angleOffset = _random.NextFloat(MathHelper.PiOver4, 3 * MathHelper.PiOver4);
        var targetAngle = spawnAngle + MathHelper.Pi + angleOffset;
        var targetDistance = _random.NextFloat(component.MinimumDistance, component.MaximumDistance);
        var targetPosition = new Vector2(
            MathF.Cos((float)targetAngle.Theta) * targetDistance,
            MathF.Sin((float)targetAngle.Theta) * targetDistance
        );

        // FTL the shuttle to the spawn coordinates
        var spawnCoords = new EntityCoordinates(mapUid.Value, spawnPosition);
        
        if (!TryComp<ShuttleComponent>(shuttleUid, out var shuttleComp))
        {
            Log.Error($"Loaded shuttle {shuttleUid} doesn't have ShuttleComponent!");
            QueueDel(shuttleUid);
            _mapManager.DeleteMap(tempMapId);
            return;
        }

        // Use TryFTLProximity to move the shuttle to the spawn location
        if (!TryComp<TransformComponent>(shuttleUid, out var shuttleXform))
        {
            Log.Error($"Loaded shuttle {shuttleUid} doesn't have TransformComponent!");
            QueueDel(shuttleUid);
            _mapManager.DeleteMap(tempMapId);
            return;
        }

        _shuttle.TryFTLProximity((shuttleUid, shuttleXform), spawnCoords);

        // Add any additional components to the shuttle
        EntityManager.AddComponents(shuttleUid, component.AddComponents);

        // Delete the temporary map
        _mapManager.DeleteMap(tempMapId);

        // Wait a brief moment for the FTL to complete, then enable autopilot
        Timer.Spawn(2000, () =>
        {
            if (!Exists(shuttleUid))
                return;

            // Get the current map coordinates of the shuttle
            if (!TryComp<TransformComponent>(shuttleUid, out var xform))
                return;

            // Set up autopilot target coordinates
            var targetCoords = new MapCoordinates(targetPosition, xform.MapID);

            // Enable autopilot
            _autopilot.EnableAutopilot(shuttleUid, targetCoords);

            Log.Info($"Hauler autopilot event: Shuttle spawned at {spawnPosition}, navigating to {targetPosition}");
        });
    }

    protected override void Ended(EntityUid uid, HaulerAutopilotRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        // Clean up the shuttle when the event ends
        if (component.ShuttleUid != null && Exists(component.ShuttleUid.Value))
        {
            // Disable autopilot if it's still active
            _autopilot.DisableAutopilot(component.ShuttleUid.Value);

            // Delete the shuttle
            QueueDel(component.ShuttleUid.Value);
        }
    }
}
