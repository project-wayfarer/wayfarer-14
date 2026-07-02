using Robust.Shared.Serialization;

namespace Content.Shared._WF.Corporations;

[Serializable, NetSerializable]
public enum CorporationRank : byte
{
    /// <summary>Standard member with no special permissions.</summary>
    Member = 0,

    /// <summary>Can send invites to other players.</summary>
    Recruiter = 1,

    /// <summary>Can edit description, change privacy, invite members, kick Members/Recruiters, and promote up to Recruiter.</summary>
    Manager = 2,

    /// <summary>Full control over the corporation. There can only be one Leader.</summary>
    Leader = 3,
}

[Serializable, NetSerializable]
public enum CorporationPrivacy : byte
{
    /// <summary>Anyone can join without an invitation.</summary>
    Public = 0,

    /// <summary>Only players with an active invite can join, and the corporation is hidden from the browse list.</summary>
    Unlisted = 1,

    /// <summary>Only players with an active invite can join, but the corporation is visible in the browse list.</summary>
    Private = 2,
}

[Serializable, NetSerializable]
public enum CorporationView : byte
{
    List = 0,
    Create = 1,
    Invite = 2,
}
