using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Shuttles.Components;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
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
    private const float BuckledSmoothingScale = 0.3f;
    private const float BuckledMaxOffsetScale = 0.4f;
    private const float CompensationOvershootAllowance = 0.2f;
    private const float RecentCorrectionDecayPerSecond = 10f;
    private const float MinimumCompensationCap = 0.03f;
    private const float MovingGridLinearVelocityThreshold = 0.05f;
    private const float MovingGridAngularVelocityThreshold = 0.05f;
    private const float PilotActivationThresholdScale = 1.1f;
    private const float PilotSmoothingScale = 0.2f;
    private const float PilotMaxSmoothingOffset = 0.08f;
    private const float PilotCompensationOvershootAllowance = 0.05f;
    private const float PilotAverageFollowPerSecond = 10f;
    private const float PilotDecayPerSecond = 8f;

    private EntityUid? _trackedUid;
    private Vector2 _lastWorldPos;
    private bool _hasLastPos;
    private Vector2 _reconcileOffset;
    private Vector2 _targetOffset;
    private float _retriggerCooldown;
    private float _returnCooldown;
    private float _recentCorrectionMagnitude;

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
            _recentCorrectionMagnitude = 0f;
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
            _recentCorrectionMagnitude = 0f;
            return;
        }

        var uid = local.Value;
        var worldPos = _transform.GetWorldPosition(uid);
        var isBuckled = TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled;
        var isPiloting = HasComp<PilotComponent>(uid);

        if (!_hasLastPos || _trackedUid != uid)
        {
            _trackedUid = uid;
            _lastWorldPos = worldPos;
            _hasLastPos = true;
            return;
        }

        if (frameTime <= 0f)
            return;

        if (isPiloting)
        {
            UpdatePilotSmoothing(uid, worldPos, frameTime);
            return;
        }

        if (IsOnMovingGrid(uid))
        {
            _reconcileOffset = Vector2.Zero;
            _targetOffset = Vector2.Zero;
            _retriggerCooldown = 0f;
            _returnCooldown = 0f;
            _recentCorrectionMagnitude = 0f;
            _lastWorldPos = worldPos;
            return;
        }

        _retriggerCooldown = MathF.Max(0f, _retriggerCooldown - frameTime);
        _returnCooldown = MathF.Max(0f, _returnCooldown - frameTime);
        _recentCorrectionMagnitude *= MathF.Exp(-RecentCorrectionDecayPerSecond * frameTime);

        var delta = worldPos - _lastWorldPos;
        var moved = delta.Length();

        var expectedMove = BaseMovementTolerance;
        if (TryComp<PhysicsComponent>(uid, out var physics))
            expectedMove += physics.LinearVelocity.Length() * frameTime * VelocityToleranceScale;

        var activationMove = expectedMove * ActivationThresholdScale;
        var maxOffset = isBuckled
            ? MaxSmoothingOffset * BuckledMaxOffsetScale
            : MaxSmoothingOffset;

        if (moved > activationMove && moved > 0.0001f)
        {
            var excess = moved - activationMove;
            var correction = delta * (excess / moved);

            if (isBuckled)
                correction *= BuckledSmoothingScale;

            var correctionLen = correction.Length();

            if (_retriggerCooldown <= 0f || correctionLen >= ForcedSmoothingCorrection)
            {
                // Accumulate into a target offset so repeated corrections steer the camera smoothly
                // instead of repeatedly pulling it back toward center between triggers.
                var compensation = -correction;

                // If incoming correction changes direction, damp existing target so stale offset
                // does not exaggerate lag during rapid direction changes.
                if (Vector2.Dot(_targetOffset, compensation) < 0f)
                    _targetOffset *= 0.4f;

                _targetOffset += compensation;
                _recentCorrectionMagnitude = MathF.Max(_recentCorrectionMagnitude, correctionLen);
                _retriggerCooldown = SmoothingRetriggerCooldown;
                _returnCooldown = SmoothingReturnDelay;
            }
        }

        // Keep smoothing effect tightly bounded to the size of recent corrections so it cannot
        // look significantly larger than the jitter being compensated.
        var compensationCap = MathF.Max(MinimumCompensationCap,
            _recentCorrectionMagnitude * (1f + CompensationOvershootAllowance));
        var dynamicCap = MathF.Min(maxOffset, compensationCap);

        var targetLen = _targetOffset.Length();
        if (targetLen > dynamicCap)
            _targetOffset = _targetOffset / targetLen * dynamicCap;

        if (_returnCooldown <= 0f)
        {
            var decayBlend = 1f - MathF.Exp(-SmoothingDecayPerSecond * frameTime);
            _targetOffset = Vector2.Lerp(_targetOffset, Vector2.Zero, decayBlend);
        }

        var followBlend = 1f - MathF.Exp(-SmoothingFollowPerSecond * frameTime);
        _reconcileOffset = Vector2.Lerp(_reconcileOffset, _targetOffset, followBlend);

        var reconcileLen = _reconcileOffset.Length();
        if (reconcileLen > dynamicCap)
            _reconcileOffset = _reconcileOffset / reconcileLen * dynamicCap;

        _lastWorldPos = worldPos;
    }

    private void UpdatePilotSmoothing(EntityUid uid, Vector2 worldPos, float frameTime)
    {
        _retriggerCooldown = MathF.Max(0f, _retriggerCooldown - frameTime);
        _returnCooldown = MathF.Max(0f, _returnCooldown - frameTime);
        _recentCorrectionMagnitude *= MathF.Exp(-RecentCorrectionDecayPerSecond * frameTime);

        var delta = worldPos - _lastWorldPos;
        var moved = delta.Length();

        var expectedMove = BaseMovementTolerance;
        if (TryComp<PhysicsComponent>(uid, out var physics))
            expectedMove += physics.LinearVelocity.Length() * frameTime * VelocityToleranceScale;

        var activationMove = expectedMove * PilotActivationThresholdScale;

        if (moved > activationMove && moved > 0.0001f)
        {
            var excess = moved - activationMove;
            var correction = delta * (excess / moved) * PilotSmoothingScale;
            var correctionLen = correction.Length();

            if (_retriggerCooldown <= 0f || correctionLen >= ForcedSmoothingCorrection * PilotSmoothingScale)
            {
                var compensation = -correction;

                // Piloting uses an averaged target instead of accumulation so rapid accel/decel
                // gets a slight damped offset without reintroducing ship jitter.
                var averageBlend = 1f - MathF.Exp(-PilotAverageFollowPerSecond * frameTime);
                _targetOffset = Vector2.Lerp(_targetOffset, compensation, averageBlend);
                _recentCorrectionMagnitude = MathF.Max(_recentCorrectionMagnitude, correctionLen);
                _retriggerCooldown = SmoothingRetriggerCooldown;
                _returnCooldown = SmoothingReturnDelay;
            }
        }

        var compensationCap = MathF.Max(MinimumCompensationCap,
            _recentCorrectionMagnitude * (1f + PilotCompensationOvershootAllowance));
        var dynamicCap = MathF.Min(PilotMaxSmoothingOffset, compensationCap);

        var targetLen = _targetOffset.Length();
        if (targetLen > dynamicCap)
            _targetOffset = _targetOffset / targetLen * dynamicCap;

        if (_returnCooldown <= 0f)
        {
            var decayBlend = 1f - MathF.Exp(-PilotDecayPerSecond * frameTime);
            _targetOffset = Vector2.Lerp(_targetOffset, Vector2.Zero, decayBlend);
        }

        _reconcileOffset = _targetOffset;
        _lastWorldPos = worldPos;
    }

    private bool IsOnMovingGrid(EntityUid uid)
    {
        var gridUid = Transform(uid).GridUid;
        if (gridUid == null)
            return false;

        if (!TryComp<PhysicsComponent>(gridUid, out var gridPhysics))
            return false;

        return gridPhysics.LinearVelocity.Length() >= MovingGridLinearVelocityThreshold ||
               MathF.Abs(gridPhysics.AngularVelocity) >= MovingGridAngularVelocityThreshold;
    }

    private void OnGetEyeOffset(EntityUid uid, ContentEyeComponent component, ref GetEyeOffsetEvent args)
    {
        if (_player.LocalEntity != uid)
            return;

        args.Offset += _reconcileOffset;
    }
}
