using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.Corporations;

// State

[Serializable, NetSerializable]
public sealed class CorpAdminEuiState : EuiStateBase
{
    public List<CorpAdminCorpData> Corporations { get; init; } = new();
}

/// <summary>Full admin-visible snapshot of a single corporation.</summary>
[Serializable, NetSerializable]
public sealed class CorpAdminCorpData
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CorporationPrivacy Privacy { get; init; }
    public int Balance { get; init; }
    public List<CorpAdminMemberData> Members { get; init; } = new();
    public CorpAdminStationData? Station { get; init; }
    /// <summary>Filenames (not full paths) of archived/deleted station saves for this corp.</summary>
    public List<string> ArchivedStationFiles { get; init; } = new();
}

[Serializable, NetSerializable]
public sealed class CorpAdminMemberData
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public CorporationRank Rank { get; init; }
}

[Serializable, NetSerializable]
public sealed class CorpAdminStationData
{
    public string StationName { get; init; } = string.Empty;
    public string SavePath { get; init; } = string.Empty;
    public bool ActiveThisRound { get; init; }
}

// Messages (client → server)

public static class CorpAdminEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Refresh : EuiMessageBase { }

    [Serializable, NetSerializable]
    public sealed class SetBalance : EuiMessageBase
    {
        public int CorporationId { get; init; }
        public int NewBalance { get; init; }
    }

    [Serializable, NetSerializable]
    public sealed class SetDescription : EuiMessageBase
    {
        public int CorporationId { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    [Serializable, NetSerializable]
    public sealed class SetPrivacy : EuiMessageBase
    {
        public int CorporationId { get; init; }
        public CorporationPrivacy Privacy { get; init; }
    }

    [Serializable, NetSerializable]
    public sealed class KickMember : EuiMessageBase
    {
        public int CorporationId { get; init; }
        public string UserId { get; init; } = string.Empty;
    }

    [Serializable, NetSerializable]
    public sealed class SetMemberRank : EuiMessageBase
    {
        public int CorporationId { get; init; }
        public string UserId { get; init; } = string.Empty;
        public CorporationRank Rank { get; init; }
    }

    [Serializable, NetSerializable]
    public sealed class DeleteCorporation : EuiMessageBase
    {
        public int CorporationId { get; init; }
    }

    [Serializable, NetSerializable]
    public sealed class EvictStation : EuiMessageBase
    {
        public int CorporationId { get; init; }
    }

    [Serializable, NetSerializable]
    public sealed class SaveStation : EuiMessageBase
    {
        public int CorporationId { get; init; }
    }

    [Serializable, NetSerializable]
    public sealed class GrantStation : EuiMessageBase
    {
        public int CorporationId { get; init; }
        public string StationName { get; init; } = string.Empty;
    }

    [Serializable, NetSerializable]
    public sealed class CreateCorporation : EuiMessageBase
    {
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public CorporationPrivacy Privacy { get; init; }
    }

    [Serializable, NetSerializable]
    public sealed class AddMember : EuiMessageBase
    {
        public int CorporationId { get; init; }
        public Guid UserId { get; init; }
    }

    [Serializable, NetSerializable]
    public sealed class RecoverStation : EuiMessageBase
    {
        public int CorporationId { get; init; }
        /// <summary>Filename (not full path) of the archived save to restore, e.g. "corp_3_55.yml".</summary>
        public string ArchiveFileName { get; init; } = string.Empty;
        public string StationName { get; init; } = string.Empty;
    }
}
