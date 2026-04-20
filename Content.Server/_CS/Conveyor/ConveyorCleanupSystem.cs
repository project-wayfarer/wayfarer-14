using Content.Server.Administration.Logs;
using Content.Shared._CS.CCVar;
using Content.Shared.Conveyor;
using Content.Shared.Database;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;

namespace Content.Server.Conveyor.EntitySystems
{
    /// <summary>
    /// Responsible for taking care of conveyor lag machines without administrative intervention.
    /// </summary>
    [UsedImplicitly]
    public sealed class ConveyorCleanupSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IAdminLogManager _adminLog = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        private TimeSpan _nextCleanup = TimeSpan.Zero;
        private TimeSpan _cleanupInterval = TimeSpan.FromSeconds(51); // Time before next cleanup. Can be tuned in cvars.
        private readonly SoundPathSpecifier _breaksound = new("/Audio/Effects/metal_crunch.ogg");
        private int _maxItemCount = 200; // Max allowed number of items on a belt before it collapses. Can be tuned in cvars.

        public override void Initialize()
        {
            base.Initialize();
            _cfg.OnValueChanged(CSCVars.ConveyorMaxItemCount, value => _maxItemCount = value, true);
            _cfg.OnValueChanged(CSCVars.ConveyorCleanupIntervalSeconds, value => _cleanupInterval = TimeSpan.FromSeconds(value), true);
        }

        public override void Update(float frameTime)
        {
            var curTime = _timing.CurTime;
            if (curTime < _nextCleanup)
                return;
            _nextCleanup = curTime + _cleanupInterval;
            var query = EntityQueryEnumerator<ConveyorComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (Deleted(uid) || Terminating(uid))
                    continue;
                var count = CountEntitiesOnConveyor(uid);
                if (count > _maxItemCount)
                {
                    QueueConveyorForDeletion(uid, count);
                }
            }
        }
        private int CountEntitiesOnConveyor(EntityUid uid)
        {
            if (!TryComp<PhysicsComponent>(uid, out var physics)) //this is so much faster than iterating through every contact.
                return 0;
            return physics.ContactCount;
        }
        private void QueueConveyorForDeletion(EntityUid uid, int itemCount)
        {
            TryComp(uid, out TransformComponent? transformComponent);
            if (transformComponent != null)
            {
                _popup.PopupCoordinates(Loc.GetString("conveyor-overload-destroyed", ("conveyor", uid)), transformComponent.Coordinates, PopupType.LargeCaution);
                _audio.PlayPvs(_breaksound, transformComponent.Coordinates);
            }
            // Log for admins
            _adminLog.Add(
                LogType.EntityDelete,
                LogImpact.Medium,
                $"Conveyor {ToPrettyString(uid)} destroyed because it had {itemCount} items on it (exceeds {_maxItemCount})");
            // Delete the conveyor
            _entityManager.QueueDeleteEntity(uid);
        }
    }
}
