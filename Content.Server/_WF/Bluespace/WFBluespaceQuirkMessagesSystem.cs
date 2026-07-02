using Content.Shared._WF.Bluespace;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._WF.Bluespace;

/// <summary>
/// Drives <see cref="WFBluespaceQuirkMessagesComponent"/>: when the timer elapses,
/// finds the player currently holding/wearing the entity (by walking the transform
/// parent chain) and shows them a random localized popup.
/// </summary>
public sealed class WFBluespaceQuirkMessagesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WFBluespaceQuirkMessagesComponent>();
        while (query.MoveNext(out var uid, out var quirk))
        {
            if (quirk.NextMessageTime == null)
            {
                quirk.NextMessageTime = now + RollInterval(quirk);
                continue;
            }

            if (now < quirk.NextMessageTime.Value)
                continue;

            // Schedule the next tick regardless of whether we found a holder,
            // so we don't burn CPU re-checking every update.
            quirk.NextMessageTime = now + RollInterval(quirk);

            if (quirk.Messages.Count == 0)
                continue;

            if (!TryFindHolder(uid, out var holder))
                continue;

            var msg = Loc.GetString(_random.Pick(quirk.Messages));
            _popup.PopupEntity(msg, holder, holder, PopupType.Medium);
        }
    }

    private TimeSpan RollInterval(WFBluespaceQuirkMessagesComponent quirk)
    {
        var min = quirk.MinInterval.TotalSeconds;
        var max = quirk.MaxInterval.TotalSeconds;
        if (max < min)
            max = min;
        return TimeSpan.FromSeconds(_random.NextDouble(min, max));
    }

    /// <summary>
    /// Walks up the transform parent chain looking for an entity controlled by
    /// a player (i.e. has an <see cref="ActorComponent"/>). This catches both
    /// "held in hand" and "worn/contained inside something the player is wearing".
    /// </summary>
    private bool TryFindHolder(EntityUid uid, out EntityUid holder)
    {
        holder = default;
        var xformQuery = GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(uid, out var xform))
            return false;

        var current = xform.ParentUid;
        var safety = 0;
        while (current.IsValid() && safety++ < 16)
        {
            if (HasComp<ActorComponent>(current))
            {
                holder = current;
                return true;
            }

            if (!xformQuery.TryGetComponent(current, out var parentXform))
                return false;

            current = parentXform.ParentUid;
        }

        return false;
    }
}
