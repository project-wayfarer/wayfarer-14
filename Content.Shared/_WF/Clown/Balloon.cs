using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.Clown;

// Marks a finished balloon animal. Networked so the client can list which held items need the floating-balloon visual.
[RegisterComponent, NetworkedComponent]
public sealed partial class BalloonOnStringComponent : Component;

// Marks an un-twisted balloon. Gates the right-click "Twist into..." verbs.
[RegisterComponent]
public sealed partial class BalloonEmptyComponent : Component;

// Do-after event raised while a clown is twisting an empty balloon. TargetPrototype is the animal chosen from the right-click menu.
[Serializable, NetSerializable]
public sealed partial class BalloonTwistDoAfterEvent : SimpleDoAfterEvent
{
    public EntProtoId TargetPrototype;
}
