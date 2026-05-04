using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.FloofStation;

[RegisterComponent, NetworkedComponent]
public sealed partial class HeldInMouthComponent : Component
{
    [DataField]
    public EntityUid Pred;

    [DataField]
    public SoundSpecifier SoundSpit = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
}
