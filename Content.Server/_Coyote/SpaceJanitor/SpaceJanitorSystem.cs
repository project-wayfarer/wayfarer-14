using System.Numerics;
using Content.Server.Storage.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Coyote.SpaceJanitor;

/// <summary>
/// This little weirdo goes through every item in space and, if its been there for a long time straight, deletes it.
/// This is to prevent space from being cluttered with debris and items that have been left behind.
/// This is a server-side system only, and does not need to be networked to clients.
/// </summary>
public sealed class SpaceJanitorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private const int MinutesBetweenChecks = 15;
    private const int MinutesBeforeCleanup = 720; // 12 hours
    private TimeSpan _nextCheck = TimeSpan.Zero;

    /// <inheritdoc/>
    public override void Initialize()
    {
        // penors
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = _gameTiming.CurTime;
        if (curTime < _nextCheck)
            return;
        _nextCheck = curTime + TimeSpan.FromMinutes(MinutesBetweenChecks);
        var query = EntityQueryEnumerator<SpaceJanitorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!NakedAndInSpace(uid, comp))
            {
                // its not in space, so reset the timer.
                ResetNakedSpaceTime(uid, comp);
                continue;
            }

            UpdateNakedSpaceTime(
                uid,
                comp,
                curTime);
            DeleteIfNeeded(
                uid,
                comp,
                curTime);
        }
    }

    /// <summary>
    /// Checks if:
    /// The entity has null grid
    /// The entity is not inside something (has actual local coordinates)
    /// </summary>
    private bool NakedAndInSpace(EntityUid uid, SpaceJanitorComponent comp)
    {
        var xform = Transform(uid);
        if (xform.LocalPosition == Vector2.Zero)
            return false;
        // clean up empty casings, whether in space or not, but only while not carried by something.
        if (comp.IsCasing
            && TryComp<CartridgeAmmoComponent>(uid, out var cartridge)
            && cartridge.Spent)
            return true; // naked, and in 'space' (on the floor)
        if (xform.GridUid != null)
            return false;
        return true;
    }

    private void UpdateNakedSpaceTime(EntityUid uid, SpaceJanitorComponent comp, TimeSpan curTime)
    {
        if (comp.FoundInSpaceTime == TimeSpan.Zero)
        {
            comp.FoundInSpaceTime = curTime;
        }
    }

    private void ResetNakedSpaceTime(EntityUid uid, SpaceJanitorComponent comp)
    {
        comp.FoundInSpaceTime = TimeSpan.Zero;
    }

    private void DeleteIfNeeded(EntityUid uid, SpaceJanitorComponent comp, TimeSpan curTime)
    {
        if (comp.FoundInSpaceTime == TimeSpan.Zero)
            return;
        if (curTime - comp.FoundInSpaceTime < TimeSpan.FromMinutes(MinutesBeforeCleanup))
            return;
        // delete the entity.
        if (TryComp<EntityStorageComponent>(uid, out var storage)
            && !storage.DeleteContentsOnDestruction)
        {
            var sess = IoCManager.Resolve<SharedEntityStorageSystem>();
            sess.EmptyContents(uid, storage);
        }
        if (TryComp<StorageComponent>(uid, out var storage2))
        {
            var storo = IoCManager.Resolve<SharedContainerSystem>();
            storo.EmptyContainer(storage2.Container);
        }
        var myCoords = Transform(uid).LocalPosition;
        Log.Info($"Space janitor sent entity {ToPrettyString(uid)} at {myCoords} to the shadow realm for being in space too long.");
        QueueDel(uid);
    }
}
