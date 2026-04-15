using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.Movement.Components;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client.Movement.Systems;

/// <summary>
/// Adds a temporary eye offset when the locally controlled entity receives a sudden positional correction.
/// This smooths visual reconciliation without changing authoritative physics.
/// </summary>
public sealed class PredictionReconcileSmoothingSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float BaseMovementTolerance = 0.08f;
    private const float VelocityToleranceScale = 1.6f;
    private const float MaxSmoothingOffset = 1.2f;
    private const float SmoothingDecayPerSecond = 12f;

    private EntityUid? _trackedUid;
    private Vector2 _lastWorldPos;
    private bool _hasLastPos;
    private Vector2 _reconcileOffset;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;

        SubscribeLocalEvent<ContentEyeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var local = _player.LocalEntity;
        if (local == null || Deleted(local.Value))
        {
            _trackedUid = null;
            _hasLastPos = false;
            _reconcileOffset = Vector2.Zero;
            return;
        }

        var uid = local.Value;
        var worldPos = _transform.GetWorldPosition(uid);

        if (!_hasLastPos || _trackedUid != uid)
        {
            _trackedUid = uid;
            _lastWorldPos = worldPos;
            _hasLastPos = true;
            return;
        }

        if (frameTime <= 0f)
            return;

        var delta = worldPos - _lastWorldPos;
        var moved = delta.Length();

        var expectedMove = BaseMovementTolerance;
        if (TryComp<PhysicsComponent>(uid, out var physics))
            expectedMove += physics.LinearVelocity.Length() * frameTime * VelocityToleranceScale;

        if (moved > expectedMove && moved > 0.0001f)
        {
            var excess = moved - expectedMove;
            var correction = delta * (excess / moved);

            // Apply inverse camera offset so large reconciliation jumps are visually blended.
            _reconcileOffset -= correction;

            var offsetLen = _reconcileOffset.Length();
            if (offsetLen > MaxSmoothingOffset)
                _reconcileOffset = _reconcileOffset / offsetLen * MaxSmoothingOffset;
        }

        var blend = 1f - MathF.Exp(-SmoothingDecayPerSecond * frameTime);
        _reconcileOffset = Vector2.Lerp(_reconcileOffset, Vector2.Zero, blend);
        _lastWorldPos = worldPos;
    }

    private void OnGetEyeOffset(EntityUid uid, ContentEyeComponent component, ref GetEyeOffsetEvent args)
    {
        if (_player.LocalEntity != uid)
            return;

        args.Offset += _reconcileOffset;
    }
}
