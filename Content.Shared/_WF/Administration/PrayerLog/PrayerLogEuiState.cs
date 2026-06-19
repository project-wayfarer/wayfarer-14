using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.Administration.PrayerLog;

[Serializable, NetSerializable]
public sealed class PrayerLogEuiState(List<PrayerLogEntry> entries) : EuiStateBase
{
    public List<PrayerLogEntry> Entries { get; } = entries;
}

[Serializable, NetSerializable]
public sealed record PrayerLogEntry(
    NetUserId UserId,
    string PlayerName,
    string CharacterName,
    string ItemName,
    string Type,
    TimeSpan RoundTime,
    string Content);

public static class PrayerLogEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Follow(NetUserId userId) : EuiMessageBase
    {
        public NetUserId UserId { get; } = userId;
    }
}
