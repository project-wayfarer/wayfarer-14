using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.RoleplayLeveling.Events;

/// <summary>
/// Event raised when a player gains experience (local event, not networked)
/// </summary>
public sealed class RoleplayExperienceGainedEvent : EntityEventArgs
{
    public EntityUid Player { get; }
    public long ExperienceAmount { get; }
    public string Reason { get; }

    public RoleplayExperienceGainedEvent(EntityUid player, long experienceAmount, string reason)
    {
        Player = player;
        ExperienceAmount = experienceAmount;
        Reason = reason;
    }
}

/// <summary>
/// Event raised when a player levels up (local event, not networked)
/// </summary>
public sealed class RoleplayLevelUpEvent : EntityEventArgs
{
    public EntityUid Player { get; }
    public int NewLevel { get; }

    public RoleplayLevelUpEvent(EntityUid player, int newLevel)
    {
        Player = player;
        NewLevel = newLevel;
    }
}

/// <summary>
/// Event raised when a player receives a commend (local event, not networked)
/// </summary>
public sealed class RoleplayCommendReceivedEvent : EntityEventArgs
{
    public EntityUid Recipient { get; }
    public EntityUid Giver { get; }
    public string? Comment { get; }
    public bool IsPrivate { get; }

    public RoleplayCommendReceivedEvent(EntityUid recipient, EntityUid giver, string? comment, bool isPrivate)
    {
        Recipient = recipient;
        Giver = giver;
        Comment = comment;
        IsPrivate = isPrivate;
    }
}

/// <summary>
/// Message sent from client to request giving a commend to another player
/// </summary>
[Serializable, NetSerializable]
public sealed class GiveCommendMessage : EntityEventArgs
{
    public NetEntity Target { get; }
    public string? Comment { get; }
    public bool IsPrivate { get; }

    public GiveCommendMessage(NetEntity target, string? comment, bool isPrivate)
    {
        Target = target;
        Comment = comment;
        IsPrivate = isPrivate;
    }
}

/// <summary>
/// Message sent from client to request available commends count
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestAvailableCommendsMessage : EntityEventArgs
{
}

/// <summary>
/// Message sent from server with available commends count
/// </summary>
[Serializable, NetSerializable]
public sealed class AvailableCommendsMessage : EntityEventArgs
{
    public int AvailableCommends { get; }
    
    public AvailableCommendsMessage(int availableCommends)
    {
        AvailableCommends = availableCommends;
    }
}

/// <summary>
/// Message sent from client to request their own recent commends
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestMyCommendsMessage : EntityEventArgs
{
}

/// <summary>
/// A single commend entry returned to the client
/// </summary>
[Serializable, NetSerializable]
public sealed class CommendEntryData
{
    public string Comment { get; }
    public string GiverName { get; }
    public bool IsPrivate { get; }
    public DateTime ReceivedAt { get; }

    public CommendEntryData(string comment, string giverName, bool isPrivate, DateTime receivedAt)
    {
        Comment = comment;
        GiverName = giverName;
        IsPrivate = isPrivate;
        ReceivedAt = receivedAt;
    }
}

/// <summary>
/// Message sent from server with the player's own recent commends
/// </summary>
[Serializable, NetSerializable]
public sealed class MyCommendsMessage : EntityEventArgs
{
    public List<CommendEntryData> Commends { get; }

    public MyCommendsMessage(List<CommendEntryData> commends)
    {
        Commends = commends;
    }
}
