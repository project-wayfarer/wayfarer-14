using Content.Shared._WF.Chat;
using Robust.Shared.GameObjects;

namespace Content.Server._WF.Chat;

public sealed class SpeakFontOverrideSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpeakFontOverrideComponent, TransformSpeechAppearanceEvent>(OnTransformSpeechAppearance);
    }

    private void OnTransformSpeechAppearance(Entity<SpeakFontOverrideComponent> ent, ref TransformSpeechAppearanceEvent ev)
    {
        if (!string.IsNullOrEmpty(ent.Comp.FontId))
            ev.FontId = ent.Comp.FontId;
        if (ent.Comp.FontSize.HasValue)
            ev.FontSize = ent.Comp.FontSize;
    }
}
