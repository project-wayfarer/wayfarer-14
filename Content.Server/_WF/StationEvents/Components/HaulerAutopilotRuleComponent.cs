using Content.Server.StationEvents.Events;
using Content.Server._WF.StationEvents.Events;
using Robust.Shared.Utility;

namespace Content.Server._WF.StationEvents.Components;

/// <summary>
/// Event component for spawning a hauler shuttle with autopilot engaged.
/// </summary>
[RegisterComponent, Access(typeof(HaulerAutopilotRuleSystem))]
public sealed partial class HaulerAutopilotRuleComponent : Component
{
    /// <summary>
    /// The path to the hauler shuttle map file.
    /// </summary>
    [DataField]
    public ResPath ShuttlePath = new("/Maps/_WF/Shuttles/Hauler/ambitionap.yml");

    /// <summary>
    /// Minimum distance to spawn the shuttle from the center of the map.
    /// </summary>
    [DataField]
    public float MinimumDistance = 8000f;

    /// <summary>
    /// Maximum distance to spawn the shuttle from the center of the map.
    /// </summary>
    [DataField]
    public float MaximumDistance = 10000f;

    /// <summary>
    /// Components to be added to the spawned shuttle.
    /// </summary>
    [DataField]
    public ComponentRegistry AddComponents = new();

    /// <summary>
    /// The spawned shuttle entity.
    /// </summary>
    [DataField]
    public EntityUid? ShuttleUid;
}
