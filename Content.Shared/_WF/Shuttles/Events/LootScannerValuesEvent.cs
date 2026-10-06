using Robust.Shared.Serialization;

namespace Content.Shared._WF.Shuttles.Events;

[Serializable, NetSerializable]
public sealed class LootScannerValuesEvent : EntityEventArgs
{
    public Dictionary<NetEntity, long> Values = new();
    public Dictionary<NetEntity, int> DcValues = new();
}
