namespace Content.Shared.SSDIndicator;
/// <summary>
/// Just tells the job system to try to reopen the job.
/// </summary>
public sealed class SSDJobReopenEvent : EntityEventArgs
{
    public EntityUid User { get; set; }

    public SSDJobReopenEvent(EntityUid user)
    {
        User = user;
    }
}
