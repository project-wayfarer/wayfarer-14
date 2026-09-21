using Content.Shared._WF.OutlawObjectives;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.OutlawObjectives;

[RegisterComponent]
public sealed partial class OutlawObjectiveComponent : Component
{
    [ViewVariables]
    public HashSet<ProtoId<OutlawObjectivePrototype>> Open = new();

    [ViewVariables]
    public HashSet<ProtoId<OutlawObjectivePrototype>> Items = new();

    [ViewVariables]
    public EntityUid? LastAttacker;

    public bool Settled => Open.Count == 0 && Items.Count == 0;
}

[RegisterComponent]
public sealed partial class OutlawObjectiveItemComponent : Component
{
    [ViewVariables]
    public EntityUid OwningCharacter;

    [ViewVariables]
    public string OwnerName = string.Empty;

    [ViewVariables]
    public ProtoId<OutlawObjectivePrototype> Objective;
}

[RegisterComponent]
public sealed partial class OutlawObjectiveSettingsComponent : Component
{
    [DataField(required: true)]
    public ProtoId<StackPrototype> StackDC;

    [DataField(required: true)]
    public ProtoId<StackPrototype> StackSpesos;

    [DataField(required: true)]
    public SoundSpecifier RewardSound = default!;

    [DataField(required: true)]
    public string ItemSlot = default!;

    [DataField(required: true)]
    public EntityWhitelist OutlawRoles = default!;

    [DataField(required: true)]
    public float GibPayoutRange;
}
