using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._WF.Weather;
using Content.Shared.GameTicking;
using Content.Shared.Random.Helpers;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._WF.Weather;

public sealed class WFWeatherSchedulerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    // Seconds to wait before trying again when no weather could be picked.
    private const float RetryDelay = 60f;

    private const float UpdateInterval = 1f;
    private float _sinceLastUpdate;

    // Only holds schedules changed during the round.
    private readonly Dictionary<string, bool> _overrides = new();

    private readonly HashSet<string> _rejectedSchedules = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PostGameMapLoad>(OnGameMapLoaded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        foreach (var schedule in _proto.EnumeratePrototypes<WFWeatherSchedulePrototype>())
        {
            if (!Validate(schedule))
                _rejectedSchedules.Add(schedule.ID);
        }
    }

    private bool Validate(WFWeatherSchedulePrototype schedule)
    {
        var valid = true;

        foreach (var map in schedule.Maps)
        {
            if (!_proto.HasIndex<GameMapPrototype>(map))
                Log.Warning($"Weather schedule {schedule.ID} names map {map}, which does not exist.");
        }

        if (schedule.Weathers.Count == 0)
        {
            Log.Error($"Weather schedule {schedule.ID} lists no weathers.");
            valid = false;
        }

        if (schedule.Gap.Min > schedule.Gap.Max)
        {
            Log.Error($"Weather schedule {schedule.ID} has a gap whose minimum is above its maximum.");
            valid = false;
        }

        foreach (var entry in schedule.Weathers)
        {
            if (!_proto.TryIndex(entry.Weather, out var announcement))
            {
                Log.Error($"Weather schedule {schedule.ID} names weather {entry.Weather}, which does not exist.");
                valid = false;
                continue;
            }

            if (!_proto.HasIndex(announcement.Weather))
            {
                Log.Error($"Weather {announcement.ID} runs {announcement.Weather}, which does not exist.");
                valid = false;
            }

            if (entry.Duration.Min > entry.Duration.Max)
            {
                Log.Error($"Weather schedule {schedule.ID} gives {entry.Weather} a duration whose minimum is above its maximum.");
                valid = false;
            }
            else if (entry.Duration.Min <= announcement.EndLead)
            {
                Log.Warning($"Weather schedule {schedule.ID} can roll a duration for {entry.Weather} shorter than its end lead, so the clearing announcement can go out the moment the weather starts.");
            }
        }

        return valid;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev) => _overrides.Clear();

    public bool IsEnabled(WFWeatherSchedulePrototype schedule)
        => _overrides.TryGetValue(schedule.ID, out var state) ? state : schedule.DefaultOn;

    public void SetEnabled(ProtoId<WFWeatherSchedulePrototype> scheduleId, bool enabled)
        => _overrides[scheduleId] = enabled;

    private void OnGameMapLoaded(PostGameMapLoad ev)
    {
        var mapUid = _map.GetMap(ev.Map);

        foreach (var schedule in _proto.EnumeratePrototypes<WFWeatherSchedulePrototype>())
        {
            if (schedule.Maps.Contains(ev.GameMap.ID))
                SetSchedule(mapUid, schedule.ID);
        }
    }

    // Puts a schedule on a map and starts its cycle from the beginning.
    public void SetSchedule(EntityUid mapUid, ProtoId<WFWeatherSchedulePrototype> scheduleId)
    {
        var schedule = _proto.Index(scheduleId);

        if (_rejectedSchedules.Contains(schedule.ID))
        {
            Log.Error($"Weather schedule {schedule.ID} has errors from startup and will not be put on a map.");
            return;
        }

        // Merging grids onto a live map raises the map load event again, which must not reset a running cycle.
        if (TryComp<WFWeatherScheduleComponent>(mapUid, out var existing) && existing.Schedule == scheduleId)
            return;

        var active = EnsureComp<WFWeatherScheduleComponent>(mapUid);
        active.Schedule = scheduleId;
        active.Phase = WFWeatherPhase.Waiting;
        active.TimeLeft = schedule.TimeUntilFirstWeather;
        active.CurrentWeather = null;
        active.CurrentDuration = 0;
        active.EndAnnounced = false;
    }

    public override void Update(float frameTime)
    {
        // Maps load during the lobby, so the countdown to the first weather would run before the round starts.
        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        _sinceLastUpdate += frameTime;
        if (_sinceLastUpdate < UpdateInterval)
            return;

        var elapsed = _sinceLastUpdate;
        _sinceLastUpdate = 0;

        var query = EntityQueryEnumerator<WFWeatherScheduleComponent, MapComponent>();
        while (query.MoveNext(out var uid, out var active, out var map))
        {
            var schedule = _proto.Index(active.Schedule);
            if (!IsEnabled(schedule))
                continue;

            Cycle(uid, active, schedule, map.MapId, elapsed);
        }
    }

    private void Cycle(EntityUid mapUid, WFWeatherScheduleComponent active, WFWeatherSchedulePrototype schedule, MapId mapId, float frameTime)
    {
        active.TimeLeft -= frameTime;

        switch (active.Phase)
        {
            case WFWeatherPhase.Waiting:
            {
                if (active.TimeLeft > 0)
                    return;

                var entry = PickEntry(schedule);
                if (entry == null || !_proto.TryIndex(entry.Weather, out var announcement))
                {
                    active.TimeLeft = RetryDelay;
                    return;
                }

                active.CurrentWeather = entry.Weather;
                active.CurrentDuration = entry.Duration.Next(_random);
                active.EndAnnounced = false;
                if (schedule.Announce)
                    Announce(mapId, announcement.StartAnnouncement, announcement);

                active.Phase = WFWeatherPhase.Announced;
                active.TimeLeft = announcement.StartLead;
                return;
            }
            case WFWeatherPhase.Announced:
            {
                if (active.TimeLeft > 0)
                    return;

                var announcement = _proto.Index(active.CurrentWeather!.Value);
                _weather.SetWeather(mapId, _proto.Index(announcement.Weather), _timing.CurTime + TimeSpan.FromSeconds(active.CurrentDuration));

                active.Phase = WFWeatherPhase.Running;
                active.TimeLeft = active.CurrentDuration;
                return;
            }
            case WFWeatherPhase.Running:
            {
                var announcement = _proto.Index(active.CurrentWeather!.Value);
                var stillRunning = TryComp<WeatherComponent>(mapUid, out var weatherComp)
                                   && weatherComp.Weather.ContainsKey(announcement.Weather);

                if (stillRunning && !active.EndAnnounced && active.TimeLeft <= announcement.EndLead)
                {
                    if (schedule.Announce)
                        Announce(mapId, announcement.EndAnnouncement, announcement);
                    active.EndAnnounced = true;
                }

                if (stillRunning && active.TimeLeft > 0)
                    return;

                active.CurrentWeather = null;
                active.CurrentDuration = 0;
                active.Phase = WFWeatherPhase.Waiting;
                active.TimeLeft = schedule.Gap.Next(_random);
                return;
            }
        }
    }

    private WFWeatherScheduleEntry? PickEntry(WFWeatherSchedulePrototype schedule)
    {
        // The roll below can still pick a zero weight, so weights of zero or less are filtered out here.
        var pickable = schedule.Weathers
            .Where(entry => entry.Weight > 0 && _proto.HasIndex(entry.Weather))
            .ToDictionary(entry => entry, entry => entry.Weight);

        return pickable.Count == 0 ? null : _random.Pick(pickable);
    }

    private void Announce(MapId mapId, LocId? message, WFWeatherAnnouncementPrototype announcement)
    {
        if (message == null)
            return;

        var filter = Filter.BroadcastMap(mapId);
        _chat.DispatchFilteredAnnouncement(
            filter,
            Loc.GetString(message.Value),
            sender: Loc.GetString(announcement.Sender),
            announcementSound: announcement.AnnouncementSound,
            colorOverride: announcement.AnnouncementColor);
    }
}
