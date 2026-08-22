using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Weather;

[Prototype("wfWeatherAnnouncement")]
public sealed partial class WFWeatherAnnouncementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<WeatherPrototype> Weather;

    [DataField(required: true)]
    public LocId Sender;

    [DataField]
    public LocId? StartAnnouncement;

    [DataField]
    public LocId? EndAnnouncement;

    // Seconds between the incoming announcement and the weather arriving.
    [DataField]
    public float StartLead = 300;

    // Seconds before the weather ends that the clearing announcement goes out.
    [DataField]
    public float EndLead = 300;

    [DataField]
    public SoundSpecifier? AnnouncementSound;

    [DataField]
    public Color AnnouncementColor = Color.Gold;
}
