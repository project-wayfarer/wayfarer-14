using System.Numerics;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.Corporations;

// Network-safe transfer objects

/// <summary>
/// Lightweight summary of a corporation sent over the network.
/// </summary>
[Serializable, NetSerializable]
public sealed class CorporationInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CorporationPrivacy Privacy { get; init; }
    public int MemberCount { get; init; }
    public int Balance { get; init; }
    public bool HasStation { get; init; }
    public string? StationName { get; init; }
    public bool StationVisible { get; init; }
    public Vector2? StationCoordinates { get; init; }
    /// <summary>Upkeep cost in spesos per 4 hours, or null if the station is not active this round.</summary>
    public int? StationUpkeepCost { get; init; }
}

/// <summary>
/// Summary of a single corporation member sent to the client.
/// </summary>
[Serializable, NetSerializable]
public sealed class CorporationMemberInfo
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public CorporationRank Rank { get; init; }
}

// BoundUserInterfaceState subclasses

/// <summary>
/// Main overview state sent when the cartridge opens or after any action.
/// </summary>
[Serializable, NetSerializable]
public sealed class CorporationListUiState : BoundUserInterfaceState
{
    /// <summary>The player's current corporation, or null if they are not in one.</summary>
    public CorporationInfo? MyCorporation { get; init; }

    /// <summary>The player's rank within their corporation. Only meaningful when MyCorporation != null.</summary>
    public CorporationRank MyRank { get; init; }

    /// <summary>Full member list for the player's corporation. Only populated when MyCorporation != null.</summary>
    public List<CorporationMemberInfo> Members { get; init; } = new();

    /// <summary>Listed corporations (public and private) excluding unlisted corporations and the player's own corporation.</summary>
    public List<CorporationInfo> PublicCorporations { get; init; } = new();

    /// <summary>Corporations that have sent this player an invite.</summary>
    public List<CorporationInfo> PendingInvites { get; init; } = new();

    /// <summary>Optional feedback/error message to display in the UI.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The player's own NetUserId string so the client can identify itself in member lists.</summary>
    public string MyUserId { get; init; } = string.Empty;

    /// <summary>Whether corporation station purchasing is currently enabled by server configuration.</summary>
    public bool StationPurchaseEnabled { get; init; } = true;
}

/// <summary>
/// State for the invite panel, carrying the list of characters currently on the station.
/// </summary>
[Serializable, NetSerializable]
public sealed class CorporationInviteUiState : BoundUserInterfaceState
{
    public List<string> AvailableCharacters { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

// CartridgeMessageEvent subclasses (client → server)

[Serializable, NetSerializable]
public sealed class CorporationRefreshMessage : CartridgeMessageEvent { }

[Serializable, NetSerializable]
public sealed class CorporationNavigateMessage : CartridgeMessageEvent
{
    public CorporationView View { get; init; }
}

[Serializable, NetSerializable]
public sealed class CorporationCreateMessage : CartridgeMessageEvent
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CorporationPrivacy Privacy { get; init; }
}

[Serializable, NetSerializable]
public sealed class CorporationJoinMessage : CartridgeMessageEvent
{
    public int CorporationId { get; init; }
}

[Serializable, NetSerializable]
public sealed class CorporationLeaveMessage : CartridgeMessageEvent { }

[Serializable, NetSerializable]
public sealed class CorporationDisbandMessage : CartridgeMessageEvent { }

[Serializable, NetSerializable]
public sealed class CorporationEditDescriptionMessage : CartridgeMessageEvent
{
    public string Description { get; init; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class CorporationSetPrivacyMessage : CartridgeMessageEvent
{
    public CorporationPrivacy Privacy { get; init; }
}

[Serializable, NetSerializable]
public sealed class CorporationSendInviteMessage : CartridgeMessageEvent
{
    public string CharacterName { get; init; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class CorporationRespondInviteMessage : CartridgeMessageEvent
{
    public int CorporationId { get; init; }
    public bool Accept { get; init; }
}

[Serializable, NetSerializable]
public sealed class CorporationKickMessage : CartridgeMessageEvent
{
    public string TargetUserId { get; init; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class CorporationChangeRankMessage : CartridgeMessageEvent
{
    public string TargetUserId { get; init; } = string.Empty;
    public CorporationRank NewRank { get; init; }
}

[Serializable, NetSerializable]
public sealed class CorporationPurchaseStationMessage : CartridgeMessageEvent
{
    public string StationName { get; init; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class CorporationToggleStationVisibilityMessage : CartridgeMessageEvent { }
