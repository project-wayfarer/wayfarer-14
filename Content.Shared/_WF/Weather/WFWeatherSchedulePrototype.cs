using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Weather;

[Prototype("wfWeatherSchedule")]
public sealed partial class WFWeatherSchedulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<WFWeatherScheduleEntry> Weathers = new();

    // Game map IDs this schedule starts on. Empty means nothing starts it automatically.
    [DataField]
    public List<string> Maps = new();

    [DataField]
    public float TimeUntilFirstWeather = 1800;

    [DataField]
    public MinMax Gap = new(3600, 7200);

    [DataField]
    public bool DefaultOn;

    [DataField]
    public bool Announce = true;
}

[DataDefinition]
public sealed partial class WFWeatherScheduleEntry
{
    [DataField(required: true)]
    public ProtoId<WFWeatherAnnouncementPrototype> Weather;

    [DataField]
    public float Weight = 1f;

    [DataField]
    public MinMax Duration = new(1200, 2100);
}
