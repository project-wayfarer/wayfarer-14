using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.Serialiser;

[RegisterComponent]
public sealed partial class SerialiserComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField(required: true)]
    public TimeSpan Delay;

    [DataField(required: true)]
    public string Prefix = string.Empty;

    [DataField]
    public SoundSpecifier? WorkingSound;

    [DataField]
    public SoundSpecifier? DenySound;

    public EntityUid? PlayingSound;
}

[Serializable, NetSerializable]
public sealed partial class SerialiseDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum SerialiserVisuals : byte
{
    Running,
}

[Serializable, NetSerializable]
public enum SerialiserVisualLayers : byte
{
    Base,
}
