using Content.Shared._WF.CCVar;
using Content.Shared.CCVar;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Network;

namespace Content.Shared.SSDIndicator;

public sealed partial class SSDIndicatorSystem : EntitySystem
{
    private float _jobReopenMinutes = 120f;
    private TimeSpan _nextJobReopenCheck = TimeSpan.Zero;
    private static readonly TimeSpan JobReopenCheckInterval = TimeSpan.FromSeconds(45);
    [Dependency] private readonly INetManager _net = default!; // Wayfarer

    private void HandleReopenJob(EntityUid uid, SSDIndicatorComponent comp)
    {
        if (!comp.IsSSD
            || comp.WentSSDAt == TimeSpan.Zero
            || comp.JobOpened)
            return;

        var curTime = _timing.CurTime;
        if (curTime < comp.WentSSDAt + TimeSpan.FromMinutes(_jobReopenMinutes))
            return;

        var ev = new SSDJobReopenEvent(uid);
        RaiseLocalEvent(uid, ev);
        comp.JobOpened = true;
    }
    private void PeriodicSSDCheckForJobReopening()
    {
        if (_net.IsServer && _timing.CurTime >= _nextJobReopenCheck)
        {
            _nextJobReopenCheck = _timing.CurTime + JobReopenCheckInterval;

            var query = EntityQueryEnumerator<SSDIndicatorComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                HandleReopenJob(uid, comp);
            }
        }
    }
}
