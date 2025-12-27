using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.FloofStation;

[RegisterComponent, NetworkedComponent]
public sealed partial class VoredComponent : Component
{
    [DataField("pred")]
    public EntityUid Pred;

    [DataField("digesting")]
    public bool Digesting;

    [DataField("accumulator")]
    public float Accumulator;

    [DataField("soundRelease")]
    public SoundSpecifier SoundRelease = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    [DataField("soundStomach")]
    public SoundSpecifier SoundStomach = new SoundPathSpecifier("/Audio/Floof/stomach_loop.ogg");
}
