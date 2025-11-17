using Robust.Shared.Serialization;

namespace Content.Shared._DV.AACTablet;

[Serializable, NetSerializable]
public sealed class AACTabletBuiState(HashSet<string> radioChannels) : BoundUserInterfaceState
{
    public HashSet<string> RadioChannels = radioChannels;
}
