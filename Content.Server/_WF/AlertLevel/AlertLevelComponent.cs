namespace Content.Server.AlertLevel;

public sealed partial class AlertLevelComponent
{
    /// <summary>
    /// Reason text attached to the current alert level, if one was given.
    /// </summary>
    [ViewVariables]
    public string? CurrentReason;

    /// <summary>
    /// Seconds until the next reminder announcement. Zero or below while the level is at its default.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float ReminderDelay;
}
