using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.StationRecords.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RegisterCrewConsoleComponent : Component
{
    public static string TargetIdSlotId = "RegisterCrewConsole-targetId";
    public static string PrivilegedIdSlotId = "RegisterCrewConsole-privilegedId";

    [DataField]
    public ItemSlot TargetIdSlot = new();

    [DataField]
    public ItemSlot PrivilegedIdSlot = new();
}

[Serializable, NetSerializable]
public sealed class RegisterCrewMessage : BoundUserInterfaceMessage
{
    public RegisterCrewMessage(string customJobTitle)
    {
        CustomJobTitle = customJobTitle;
    }

    public readonly string CustomJobTitle;
}

[Serializable, NetSerializable]
public sealed class RemoveCrewMessage : BoundUserInterfaceMessage
{
    public RemoveCrewMessage(uint recordId)
    {
        RecordId = recordId;
    }

    public readonly uint RecordId;
}
