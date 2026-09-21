using System.Numerics;
using Content.Shared._WF.Mime;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._WF.Mime;

public sealed class FingerGunOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private FingerGunOverlay _instance = default!;

    // Where the effect is heading. Each hit pushes it up, and it drains on its own.
    private float _target;
    private float _riseSeconds;
    private float _fadeSeconds;

    public override void Initialize()
    {
        base.Initialize();
        _instance = new FingerGunOverlay();
        SubscribeNetworkEvent<FingerGunShotEvent>(OnShot);
    }

    // The overlay manager outlives this system, so a disconnect mid-fade would leave the red stuck on screen.
    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<FingerGunOverlay>();
    }

    private void OnShot(FingerGunShotEvent ev)
    {
        _target = Math.Min(1f, _target + ev.Intensity);
        _riseSeconds = ev.RiseSeconds;
        _fadeSeconds = ev.FadeSeconds;
        _instance.HitColor = ev.Color;

        if (!_overlay.HasOverlay<FingerGunOverlay>())
            _overlay.AddOverlay(_instance);
    }

    public override void FrameUpdate(float frameTime)
    {
        if (!_overlay.HasOverlay<FingerGunOverlay>())
            return;

        _target = Math.Max(0f, _target - frameTime / _fadeSeconds);

        _instance.Level = _instance.Level < _target
            ? Math.Min(_target, _instance.Level + frameTime / _riseSeconds)
            : _target;

        // Checking only the drawn level would throw away a hit that has not started climbing yet.
        if (_target <= 0f && _instance.Level <= 0f)
            _overlay.RemoveOverlay(_instance);
    }
}

// Red vignette on the target's screen after getting "shot."
// Reuses the same GradientCircleMask shader as the real brute damage overlay.
public sealed class FingerGunOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _shader;

    public Color HitColor;
    public float Level;

    public FingerGunOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return;

        if (args.Viewport.Eye != eyeComp.Eye)
            return;

        if (Level <= 0f)
            return;

        var viewport = args.WorldAABB;
        var handle = args.WorldHandle;
        var distance = args.ViewportBounds.Width;

        var outerRadius = 2.0f * distance - Level * (2.0f - 0.8f) * distance;
        var innerRadius = 0.6f * distance - Level * (0.6f - 0.2f) * distance;

        _shader.SetParameter("time", Level);
        _shader.SetParameter("color", new Vector3(HitColor.R, HitColor.G, HitColor.B));
        _shader.SetParameter("darknessAlphaOuter", 0.8f);
        _shader.SetParameter("outerCircleRadius", outerRadius);
        _shader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
        _shader.SetParameter("innerCircleRadius", innerRadius);
        _shader.SetParameter("innerCircleMaxRadius", innerRadius + 0.02f * distance);
        handle.UseShader(_shader);
        handle.DrawRect(viewport, Color.White);
        handle.UseShader(null);
    }
}
