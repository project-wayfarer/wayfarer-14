using System.Linq;
using Content.Shared._WF.OutlawObjectives;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public readonly record struct OutlawObjectiveEntry(
    string Target,
    ProtoId<OutlawObjectivePrototype> Objective,
    bool IsSsd);

[Serializable, NetSerializable]
public sealed class OutlawObjectivesUiState(List<OutlawObjectiveEntry> entries) : BoundUserInterfaceState
{
    public readonly List<OutlawObjectiveEntry> Entries = entries;

    // Without this, an identical list counts as a change and is sent to every open app again.
    public override bool Equals(object? obj)
    {
        return obj is OutlawObjectivesUiState other && Entries.SequenceEqual(other.Entries);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var entry in Entries)
            hash.Add(entry);

        return hash.ToHashCode();
    }
}
