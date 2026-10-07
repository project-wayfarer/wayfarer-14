using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.IPC;

[Serializable, NetSerializable]
public sealed partial class PowerCellBodyDrinkDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class RobotBodyRebootDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class RepairableEyesDoAfterEvent : SimpleDoAfterEvent;
