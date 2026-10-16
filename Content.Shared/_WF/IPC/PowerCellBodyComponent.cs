using Content.Shared.Alert;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._WF.IPC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PowerCellBodyComponent : Component
{
    [DataField(required: true)]
    public float DrainRate;

    [DataField(required: true)]
    public float ColdDrainReduction;

    [DataField(required: true)]
    public float MaxHeatMultiplier;

    [DataField(required: true)]
    public float HeatDrainCapDivisor;

    [DataField(required: true)]
    public float ComfortBandFraction;

    [DataField(required: true)]
    public TimeSpan OverheatInterval;

    [DataField(required: true)]
    public float OverheatChanceDivisor;

    [DataField(required: true)]
    public float OverheatFireStacks;

    [DataField]
    public bool DrainWhenDead;

    [DataField]
    public bool DrainWithoutMind;

    [DataField(required: true)]
    public int ChargeLevels;

    [DataField(required: true)]
    public Dictionary<int, float> SpeedModifierThresholds = new();

    [DataField(required: true)]
    public ProtoId<AlertPrototype> BatteryAlert;

    [DataField(required: true)]
    public ProtoId<AlertPrototype> NoBatteryAlert;

    [DataField]
    public bool BlockSelfCellAccess;

    [DataField]
    public bool CanDrinkFromApc;

    [DataField]
    public TimeSpan DrinkDelay;

    [DataField]
    public float DrinkRange;

    [DataField]
    public float DrinkAmount;

    [DataField]
    public SoundSpecifier? DrinkSound;

    [DataField]
    public EntProtoId? DrinkEffect;

    [DataField]
    public SpriteSpecifier? DrinkVerbIcon;

    [DataField(required: true)]
    public TimeSpan UpdateInterval;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables, AutoNetworkedField]
    public int ChargeLevel;

    [ViewVariables]
    public bool HasCell;

    [ViewVariables]
    public float OverheatAccumulator;
}
