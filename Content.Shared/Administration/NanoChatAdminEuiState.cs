using Content.Shared._DeltaV.CartridgeLoader.Cartridges;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
/// State for the admin NanoChat viewer EUI
/// </summary>
[Serializable, NetSerializable]
public sealed class NanoChatAdminEuiState : EuiStateBase
{
    /// <summary>
    /// List of all NanoChat cards in the game with their data
    /// </summary>
    public List<NanoChatCardData> Cards { get; set; } = new();
}

/// <summary>
/// Represents a NanoChat card and all its messages
/// </summary>
[Serializable, NetSerializable]
public sealed class NanoChatCardData
{
    /// <summary>
    /// The entity ID of the card
    /// </summary>
    public NetEntity CardEntity { get; set; }

    /// <summary>
    /// The NanoChat number assigned to this card
    /// </summary>
    public uint? Number { get; set; }

    /// <summary>
    /// Name of the card owner (from ID card)
    /// </summary>
    public string OwnerName { get; set; } = "Unknown";

    /// <summary>
    /// Username of the player who currently owns/controls this card
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Username of the original owner whose name is on the ID card (for detecting stolen PDA usage)
    /// </summary>
    public string? OriginalOwnerUsername { get; set; }

    /// <summary>
    /// Job title of the card owner
    /// </summary>
    public string? JobTitle { get; set; }

    /// <summary>
    /// All recipients on this card
    /// </summary>
    public Dictionary<uint, NanoChatRecipient> Recipients { get; set; } = new();

    /// <summary>
    /// All messages on this card, keyed by recipient number
    /// </summary>
    public Dictionary<uint, List<NanoChatMessage>> Messages { get; set; } = new();
}

/// <summary>
/// Messages for the NanoChat admin viewer
/// </summary>
public static class NanoChatAdminEuiMsg
{
    /// <summary>
    /// Request to refresh the data
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class Refresh : EuiMessageBase
    {
    }

    /// <summary>
    /// Request to select a specific card
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class SelectCard : EuiMessageBase
    {
        public NetEntity CardEntity { get; set; }
    }
}
