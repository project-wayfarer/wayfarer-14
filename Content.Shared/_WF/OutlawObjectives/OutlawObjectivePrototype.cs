using Content.Shared._DV.Traits;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.OutlawObjectives;

public enum OutlawObjectiveTrigger : byte
{
    Critical,
    Gibbed,
    ItemSold,
}

[Prototype]
public sealed partial class OutlawObjectivePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public int RewardDC;

    [DataField(required: true)]
    public int RewardSpesos;

    [DataField(required: true)]
    public ProtoId<TraitPrototype> Trait;

    [DataField(required: true)]
    public OutlawObjectiveTrigger Trigger;

    [DataField(required: true)]
    public LocId Title;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public EntProtoId? Item;
}
