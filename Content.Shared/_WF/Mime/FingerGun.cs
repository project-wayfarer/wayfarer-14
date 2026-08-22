using Robust.Shared.Serialization;

namespace Content.Shared._WF.Mime;

[RegisterComponent]
public sealed partial class FingerGunComponent : Component;

[RegisterComponent]
public sealed partial class FingerGunBulletComponent : Component
{
    // Colours both the flash over the target and the edge on their screen.
    [DataField(required: true)]
    public Color HitColor;

    // How much of the screen edge one hit adds, where 1 is as red as it gets.
    [DataField(required: true)]
    public float HitIntensity;

    // Seconds the edge takes to climb to where a hit put it.
    [DataField(required: true)]
    public float RiseSeconds;

    // Seconds a full strength edge takes to drain back to nothing.
    [DataField(required: true)]
    public float FadeSeconds;
}

// Sent to the target's client only, telling it to play the bloody-screen vignette.
[Serializable, NetSerializable]
public sealed class FingerGunShotEvent : EntityEventArgs
{
    public float Intensity;
    public float RiseSeconds;
    public float FadeSeconds;
    public Color Color;
}
