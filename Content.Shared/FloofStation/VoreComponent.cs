using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.FloofStation;

[RegisterComponent, NetworkedComponent]
public sealed partial class VoreComponent : Component
{
    [ViewVariables]
    public Container Stomach = default!;

    [DataField("soundDevour")]
    public SoundSpecifier SoundDevour = new SoundPathSpecifier("/Audio/Floof/gulp.ogg");

    [DataField("delay")]
    public float Delay = 3f;
}
