using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Examine;
using Content.Shared._WF.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._WF.Shuttles.Systems;

public sealed class DockTimestampSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DockingComponent, DockEvent>(OnDock);
        SubscribeLocalEvent<DockingComponent, UndockEvent>(OnUndock);

        SubscribeLocalEvent<DockTimestampComponent, ExaminedEvent>(OnExamined);
    }

    private void OnDock(Entity<DockingComponent> ent, ref DockEvent args)
    {
        // Make sure we only track airlock type airlocks... Could be problematic if not.
        if (!ent.Comp.DockType.HasFlag(DockType.Airlock))
            return;

        // Add the timestamp component if it doesn't exist yet (first dock)
        var timestamp = EnsureComp<DockTimestampComponent>(ent);
        timestamp.DockStartTime = _timing.CurTime;
        Dirty(ent, timestamp); // Why do I always feel dirty using Dirty()?
    }

    private void OnUndock(Entity<DockingComponent> ent, ref UndockEvent args)
    {
        if (!TryComp<DockTimestampComponent>(ent, out var timestamp))
            return;

        timestamp.DockStartTime = null;
        Dirty(ent, timestamp);
    }

    private void OnExamined(Entity<DockTimestampComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.DockStartTime is not { } startTime)
            return;

        var elapsed = _timing.CurTime - startTime;
        var timeString = FormatDuration(elapsed);

        var msg = new FormattedMessage();
        args.PushMarkup(Loc.GetString("dock-timestamp-examine", ("time", timeString)), -111);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays}d {duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
    }
}
