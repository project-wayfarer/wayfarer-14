using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Robust.Shared.Configuration;
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
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float BaseMovementTolerance = 0.05f;
    private const float VelocityToleranceScale = 1.35f;
    private const float ActivationThresholdScale = 0.75f;
    private const float MaxSmoothingOffset = 1.2f;
    private const float SmoothingFollowPerSecond = 18f;
    private const float SmoothingDecayPerSecond = 5f;
    private const float SmoothingRetriggerCooldown = 0.08f;
    private const float SmoothingReturnDelay = 0.12f;
    private const float ForcedSmoothingCorrection = 0.25f;

    private EntityUid? _trackedUid;
    private Vector2 _lastWorldPos;
    private bool _hasLastPos;
    private Vector2 _reconcileOffset;
    private Vector2 _targetOffset;
    private float _retriggerCooldown;
    private float _returnCooldown;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;

        SubscribeLocalEvent<ContentEyeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_cfg.GetCVar(CCVars.DisableVisualSmoothingEffect))
        {
            _reconcileOffset = Vector2.Zero;
            _targetOffset = Vector2.Zero;
            _retriggerCooldown = 0f;
            _returnCooldown = 0f;
            _trackedUid = _player.LocalEntity;

            if (_trackedUid is { } tracked && !Deleted(tracked))
            {
                _lastWorldPos = _transform.GetWorldPosition(tracked);
                _hasLastPos = true;
            }
            else
            {
                _hasLastPos = false;
            }

            return;
        }

        var local = _player.LocalEntity;
        if (local == null || Deleted(local.Value))
        {
            _trackedUid = null;
            _hasLastPos = false;
            _reconcileOffset = Vector2.Zero;
            _targetOffset = Vector2.Zero;
            _retriggerCooldown = 0f;
            _returnCooldown = 0f;
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

        _retriggerCooldown = MathF.Max(0f, _retriggerCooldown - frameTime);
        _returnCooldown = MathF.Max(0f, _returnCooldown - frameTime);

        var delta = worldPos - _lastWorldPos;
        var moved = delta.Length();

        var expectedMove = BaseMovementTolerance;
        if (TryComp<PhysicsComponent>(uid, out var physics))
            expectedMove += physics.LinearVelocity.Length() * frameTime * VelocityToleranceScale;

        var activationMove = expectedMove * ActivationThresholdScale;

        if (moved > activationMove && moved > 0.0001f)
        {
            var excess = moved - activationMove;
            var correction = delta * (excess / moved);
            var correctionLen = correction.Length();

            if (_retriggerCooldown <= 0f || correctionLen >= ForcedSmoothingCorrection)
            {
                // Accumulate into a target offset so repeated corrections steer the camera smoothly
                // instead of repeatedly pulling it back toward center between triggers.
                _targetOffset -= correction;
                _retriggerCooldown = SmoothingRetriggerCooldown;
                _returnCooldown = SmoothingReturnDelay;
            }

            var offsetLen = _targetOffset.Length();
            if (offsetLen > MaxSmoothingOffset)
                _targetOffset = _targetOffset / offsetLen * MaxSmoothingOffset;
        }

        if (_returnCooldown <= 0f)
        {
            var decayBlend = 1f - MathF.Exp(-SmoothingDecayPerSecond * frameTime);
            _targetOffset = Vector2.Lerp(_targetOffset, Vector2.Zero, decayBlend);
        }

        var followBlend = 1f - MathF.Exp(-SmoothingFollowPerSecond * frameTime);
        _reconcileOffset = Vector2.Lerp(_reconcileOffset, _targetOffset, followBlend);
        _lastWorldPos = worldPos;
    }

    private void OnGetEyeOffset(EntityUid uid, ContentEyeComponent component, ref GetEyeOffsetEvent args)
    {
        if (_player.LocalEntity != uid)
            return;

        args.Offset += _reconcileOffset;
    }
}
