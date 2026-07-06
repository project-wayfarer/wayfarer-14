using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._WF.Corporations;

[NetSerializable, Serializable]
public enum CorporationAtmUiKey : byte
{
    Key
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CorporationAtmComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("cashType", customTypeSerializer: typeof(PrototypeIdSerializer<StackPrototype>))]
    public string CashType = "Credit";

    public static string CashSlotId = "corp-ATM-cashSlot";

    [DataField]
    public ItemSlot CashSlot = new();

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}

[Serializable, NetSerializable]
public sealed class CorporationAtmUiState : BoundUserInterfaceState
{
    /// <summary>Corporation name, or null if player has no corporation.</summary>
    public string? CorporationName;
    /// <summary>Corporation ID, or -1 if none.</summary>
    public int CorporationId;
    /// <summary>Current balance in spesos.</summary>
    public int Balance;
    /// <summary>Whether the player can withdraw (Manager or Leader).</summary>
    public bool CanWithdraw;
    /// <summary>Amount of cash physically inserted in the slot. -1 = wrong cash type, 0 = empty.</summary>
    public int Deposit;
    /// <summary>Error/status message loc key, or empty string.</summary>
    public string StatusMessage;

    public CorporationAtmUiState(string? corporationName, int corporationId, int balance, bool canWithdraw, int deposit, string statusMessage)
    {
        CorporationName = corporationName;
        CorporationId = corporationId;
        Balance = balance;
        CanWithdraw = canWithdraw;
        Deposit = deposit;
        StatusMessage = statusMessage;
    }
}

[Serializable, NetSerializable]
public sealed class CorporationAtmDepositMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class CorporationAtmWithdrawMessage : BoundUserInterfaceMessage
{
    public int Amount;
    public CorporationAtmWithdrawMessage(int amount) => Amount = amount;
}
