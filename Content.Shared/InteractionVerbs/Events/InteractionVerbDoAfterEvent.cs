using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs.Events;

[Serializable, NetSerializable]
public sealed partial class InteractionVerbDoAfterEvent : SimpleDoAfterEvent
{
    public NetEntity Target;
    public string VerbPrototype;

    public InteractionVerbDoAfterEvent(NetEntity target, string verbPrototype)
    {
        Target = target;
        VerbPrototype = verbPrototype;
    }
}
