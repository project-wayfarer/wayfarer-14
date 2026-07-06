using Robust.Shared.Player;

namespace Content.Server.AlertLevel;

public sealed partial class AlertLevelSystem
{
    private const float ReminderInterval = 3600f;

    private string ApplyAlertReason(AlertLevelComponent component, string level, string? reason, string announcement)
    {
        var isDefault = level == component.AlertLevels!.DefaultLevel;
        component.CurrentReason = isDefault ? null : reason;
        component.ReminderDelay = isDefault ? 0f : ReminderInterval;

        if (isDefault || string.IsNullOrEmpty(reason))
            return announcement;

        return $"{announcement} {Loc.GetString("alert-level-reason", ("reason", reason))}";
    }

    private void UpdateAlertReminder(AlertLevelComponent alert, float frameTime)
    {
        if (alert.ReminderDelay <= 0f)
            return;

        alert.ReminderDelay -= frameTime;
        if (alert.ReminderDelay > 0f)
            return;

        alert.ReminderDelay = ReminderInterval;

        if (alert.AlertLevels == null || !alert.AlertLevels.Levels.TryGetValue(alert.CurrentLevel, out var detail))
            return;

        var name = alert.CurrentLevel.ToLower();
        if (Loc.TryGetString($"alert-level-{alert.CurrentLevel}", out var locName))
            name = locName.ToLower();

        var announcement = alert.CurrentReason;
        if (string.IsNullOrEmpty(announcement) && Loc.TryGetString(detail.Announcement, out var locAnnouncement))
            announcement = locAnnouncement;

        var filter = Filter.BroadcastMap(_ticker.DefaultMap);
        _chatSystem.DispatchFilteredAnnouncement(filter,
            Loc.GetString("alert-level-reminder", ("name", name), ("announcement", announcement ?? string.Empty)),
            playSound: false,
            colorOverride: detail.Color);
    }
}
