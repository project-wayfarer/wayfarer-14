namespace Content.Shared.Storage.EntitySystems;

public sealed class FeedProduceEvent(EntityUid used)
{
    public bool Handled = false;
    public EntityUid Used { get; } = used;
}
