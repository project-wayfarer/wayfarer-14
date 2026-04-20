using Robust.Shared.GameStates;

namespace Content.Shared._NF.Bank.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class BankAccountComponent : Component
{
    // The amount of money this entity has in their bank account.
    // Should not be modified directly, may be out-of-date.
    [DataField, Access(typeof(SharedBankSystem))]
    [AutoNetworkedField]
    public int Balance;

    // Server-only: the player preferences slot index this bank account corresponds to.
    // Set at spawn time so bank operations target the correct character's account
    // regardless of which character is currently selected in the lobby.
    // -1 means unset; fall back to prefs.SelectedCharacter.
    public int CharacterSlot = -1;
}
