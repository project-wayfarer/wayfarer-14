using Content.Shared.CrewManifest;
using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class CrewManifestUiState : BoundUserInterfaceState
{
    // public string StationName; // Coyote: remove name
    public CrewManifestEntries? Entries;

    public CrewManifestUiState(CrewManifestEntries? entries) // Coyote: remove name
    {
        // StationName = stationName;  // Coyote: remove name
        Entries = entries;
    }
}
