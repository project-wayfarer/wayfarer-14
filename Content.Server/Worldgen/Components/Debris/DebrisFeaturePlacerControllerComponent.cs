using System.Numerics;
using Content.Server.Worldgen.Prototypes;
using Content.Server.Worldgen.Systems.Debris;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Worldgen.Components.Debris;

/// <summary>
///     Represents a debris entity waiting to be spawned.
/// </summary>
public struct PendingDebrisSpawn
{
    public Vector2 Point;
    public string DebrisProto;
    public EntityCoordinates Coords;
    public EntityUid ControllerUid;
    public EntityUid ChunkUid;
}

/// <summary>
///     This is used for controlling the debris feature placer.
/// </summary>
[RegisterComponent]
[Access(typeof(DebrisFeaturePlacerSystem))]
public sealed partial class DebrisFeaturePlacerControllerComponent : Component
{
    /// <summary>
    ///     Whether or not to clip debris that would spawn at a location that has a density of zero.
    /// </summary>
    [DataField("densityClip")] public bool DensityClip = true;

    /// <summary>
    ///     Whether or not entities are already spawned.
    /// </summary>
    public bool DoSpawns = true;

    [DataField("ownedDebris")]
    public Dictionary<Vector2, EntityUid?> OwnedDebris = new();

    /// <summary>
    ///     Queue of pending debris spawns to be processed gradually across ticks.
    /// </summary>
    [DataField("pendingSpawns")]
    public Queue<PendingDebrisSpawn> PendingSpawns = new();

    /// <summary>
    ///     Queue of debrises that are scheduled to be despawned.
    /// </summary>
    [DataField("pendingDeSpawns")]
    public Queue<(Vector2, EntityUid, EntityUid)> PendingDeSpawns = new();

    /// <summary>
    ///     The chance spawning a piece of debris will just be cancelled randomly.
    /// </summary>
    [DataField("randomCancelChance")] public float RandomCancellationChance = 0.35f;

    /// <summary>
    ///     Radius in which there should be no objects for debris to spawn.
    /// </summary>
    [DataField("safetyZoneRadius")] public float SafetyZoneRadius = 24.0f;

    /// <summary>
    ///     The noise channel to use as a density controller.
    /// </summary>
    [DataField("densityNoiseChannel", customTypeSerializer: typeof(PrototypeIdSerializer<NoiseChannelPrototype>))]
    public string DensityNoiseChannel { get; private set; } = default!;
}

