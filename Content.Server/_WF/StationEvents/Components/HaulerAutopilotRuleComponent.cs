using Content.Server.StationEvents.Events;
using Content.Server._WF.StationEvents.Events;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
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
    public ResPath ShuttlePath = new("/Maps/_WF/ShuttleEvent/armoredtransport.yml");

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

    /// <summary>
    /// Tracks players near the shuttle and when they entered proximity.
    /// </summary>
    public Dictionary<EntityUid, TimeSpan> PlayersNearShuttle = new();

    /// <summary>
    /// Distance threshold for proximity detection (in units).
    /// </summary>
    [DataField]
    public float ProximityDistance = 500f;

    /// <summary>
    /// Time required in proximity before playing audio (in seconds).
    /// </summary>
    [DataField]
    public float ProximityTimeRequired = 20f;

    /// <summary>
    /// The sound to play when a player stays near the shuttle.
    /// </summary>
    [DataField]
    public SoundSpecifier ProximitySound = new SoundPathSpecifier("/Audio/_WF/Music/HaulerBreach.ogg");

    /// <summary>
    /// Tracks which players have already heard the audio (to avoid repeating).
    /// </summary>
    public HashSet<EntityUid> PlayersWhoHeardAudio = new();

    /// <summary>
    /// Whether the 2-minute FTL warning announcement has already been sent.
    /// </summary>
    public bool WarningAnnouncementSent;
}
