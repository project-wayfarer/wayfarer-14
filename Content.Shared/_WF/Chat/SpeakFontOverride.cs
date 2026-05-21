using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._WF.Chat;

// Raised on a speaker before their speech bubble is built. A handler may set
// FontId or FontSize to override the speech font (see ChatSystem.SendEntitySpeak).
public sealed class TransformSpeechAppearanceEvent : EntityEventArgs
{
    public string? FontId;
    public int? FontSize;
}

[RegisterComponent]
public sealed partial class SpeakFontOverrideComponent : Component
{
    [DataField]
    public string FontId = string.Empty;

    [DataField]
    public int? FontSize;
}
