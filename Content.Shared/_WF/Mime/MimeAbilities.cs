using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Mime;

[RegisterComponent]
public sealed partial class MimeAbilitiesComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Actions = new();

    public List<EntityUid> Granted = new();
}

// Goes on a mime action. The item stays in a container on the action between summons.
[RegisterComponent]
public sealed partial class MimeSummonActionComponent : Component
{
    [DataField(required: true)]
    public EntProtoId ItemId;

    // Message prefixes. The code adds -self and -others.
    [DataField(required: true)]
    public string SummonMessage = string.Empty;

    [DataField(required: true)]
    public string PutAwayMessage = string.Empty;

    [DataField]
    public EntityUid? Item;

    public const string ContainerId = "wf-mime-summon-item";
}

[ByRefEvent]
public sealed partial class MimeSummonActionEvent : InstantActionEvent;

[RegisterComponent]
public sealed partial class InvisibleBoxComponent : Component;
