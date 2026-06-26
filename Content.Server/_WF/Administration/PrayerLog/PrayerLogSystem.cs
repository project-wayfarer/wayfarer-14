using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Shared._WF.Administration.PrayerLog;
using Content.Shared._WF.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._WF.Administration.PrayerLog;

public sealed class PrayerLogSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetConfigurationManager _netConfig = default!;

    private const int MaxEntries = 200;

    private readonly List<PrayerLogEntry> _entries = new();

    public event Action? EntryRecorded;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminPrayerEvent>(OnAdminPrayer);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _entries.Clear();
            EntryRecorded?.Invoke();
        });
    }

    private void OnAdminPrayer(AdminPrayerEvent ev)
    {
        Record(ev.Sender, ev.Prayable, Loc.GetString(ev.NotificationPrefix), ev.Message);

        // Play the admin-help sound for admins who have prayer alerts turned on.
        var alertedAdmins = _adminManager.ActiveAdmins
            .Where(s => _netConfig.GetClientCVar(s.Channel, WFCVars.AdminPrayerAlert));
        _audio.PlayGlobal(
            "/Audio/Effects/adminhelp.ogg",
            Filter.Empty().AddPlayers(alertedAdmins),
            false,
            AudioParams.Default);
    }

    public void Record(ICommonSession sender, EntityUid prayable, string type, string content)
    {
        var characterName = sender.AttachedEntity is { } attached ? Name(attached) : string.Empty;

        _entries.Add(new PrayerLogEntry(
            sender.UserId,
            sender.Name,
            characterName,
            Name(prayable),
            type,
            _gameTicker.RoundDuration(),
            content));

        if (_entries.Count > MaxEntries)
            _entries.RemoveAt(0);

        EntryRecorded?.Invoke();
    }

    public List<PrayerLogEntry> GetEntries()
    {
        var copy = new List<PrayerLogEntry>(_entries);
        copy.Reverse();
        return copy;
    }
}

/// <summary>
/// Raised when a player sends a prayer or call, so the prayer log can record it and alert admins.
/// </summary>
public sealed class AdminPrayerEvent(ICommonSession sender, EntityUid prayable, string notificationPrefix, string message)
{
    public ICommonSession Sender { get; } = sender;
    public EntityUid Prayable { get; } = prayable;

    /// The localization key for the prayer type, e.g. the bible or the call button.
    public string NotificationPrefix { get; } = notificationPrefix;

    public string Message { get; } = message;
}
