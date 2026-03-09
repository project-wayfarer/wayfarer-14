using Content.Shared.CrewManifest;
using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class CrewManifestUiState : BoundUserInterfaceState
{
    // public string StationName; // coyote: remove name
    public CrewManifestEntries? Entries;

    public CrewManifestUiState(CrewManifestEntries? entries) // coyote: remove name
    {
        // StationName = stationName;  // coyote: remove name
        Entries = entries;
    }
}
