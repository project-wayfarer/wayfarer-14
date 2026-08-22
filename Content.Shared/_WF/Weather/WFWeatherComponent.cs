using Content.Shared.Weather;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._WF.Weather;

[RegisterComponent, UnsavedComponent, NetworkedComponent]
public sealed partial class WFWeatherComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<WeatherPrototype>, WFWeatherData> Weather = new();

    public static readonly TimeSpan StartupTime = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan ShutdownTime = TimeSpan.FromSeconds(15);
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class WFWeatherData
{
    [NonSerialized]
    public EntityUid? Stream;

    // Each weather muffles its own sound, so two running at once do not overwrite each other.
    [NonSerialized]
    public float OcclusionTarget;

    [NonSerialized]
    public float OcclusionTimer;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan StartTime = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? EndTime;

    [ViewVariables]
    public TimeSpan Duration => EndTime == null ? TimeSpan.MaxValue : EndTime.Value - StartTime;

    [DataField]
    public WFWeatherState State = WFWeatherState.Invalid;
}

public enum WFWeatherState : byte
{
    Invalid = 0,
    Starting,
    Running,
    Ending,
}
