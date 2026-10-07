using Content.Shared.Damage.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._WF.IPC;

[RegisterComponent, NetworkedComponent]
public sealed partial class RobotBodyComponent : Component
{
    [DataField]
    public bool CanSleep;

    [DataField]
    public bool BlockBedHealing;

    [DataField]
    public ProtoId<DamageTypePrototype>? SuicideDamageType;

    [DataField]
    public bool CanReboot;

    [DataField]
    public TimeSpan RebootDelay;

    [DataField]
    public SoundSpecifier? RebootSound;

    [DataField]
    public SoundSpecifier? RebootButtonSound;

    [DataField]
    public SoundSpecifier? RebootFailSound;

    [DataField]
    public SpriteSpecifier? RebootVerbIcon;

    [DataField]
    public bool DischargeWhenShockedDead;

    [DataField]
    public float DischargeRange;

    [DataField]
    public int DischargeBolts;

    [DataField]
    public float BuzzDamageFraction;

    [DataField]
    public TimeSpan BuzzCheckInterval;

    [DataField]
    public TimeSpan BuzzCooldown;

    [DataField]
    public SoundSpecifier? BuzzSound;

    [DataField]
    public EntProtoId? SparkEffect;

    [DataField]
    public int EmpShockDamage;

    [DataField]
    public TimeSpan EmpShockTime;

    [DataField]
    public bool KeysFollowLock;

    [DataField]
    public bool WarnOnPanelTamper;

    [DataField]
    public string? KeySourceSlot;

    [DataField]
    public bool RepeatRepair;

    [ViewVariables]
    public TimeSpan BuzzAccumulator;

    [ViewVariables]
    public TimeSpan NextBuzz;
}
