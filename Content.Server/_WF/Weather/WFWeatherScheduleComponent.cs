using Content.Shared._WF.Weather;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.Weather;

[RegisterComponent, UnsavedComponent, Access(typeof(WFWeatherSchedulerSystem))]
public sealed partial class WFWeatherScheduleComponent : Component
{
    [DataField]
    public ProtoId<WFWeatherSchedulePrototype> Schedule;

    [DataField]
    public WFWeatherPhase Phase = WFWeatherPhase.Waiting;

    [DataField]
    public float TimeLeft;

    [DataField]
    public ProtoId<WFWeatherAnnouncementPrototype>? CurrentWeather;

    // Rolled when the weather is picked.
    [DataField]
    public float CurrentDuration;

    [DataField]
    public bool EndAnnounced;
}

public enum WFWeatherPhase : byte
{
    Waiting,
    Announced,
    Running,
}
