using Content.Client.Instruments;

namespace Content.Client._WF.Instruments;

/// <summary>
///     Stores the MIDI playlist state for each instrument on the client.
///     State is kept here rather than on the component so it survives when the playlist
///     window closes and reopens, and does not get replicated to the server.
/// </summary>
/// <remarks>
///     Entries are removed on entity termination, not PVS exit. Bounds dictionary size
///     per session and preserves playlists across PVS round-trips.
/// </remarks>
public sealed class PlaylistSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, PlaylistData> _playlists = new();

    public event Action<EntityUid>? PlaylistChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InstrumentComponent, EntityTerminatingEvent>(OnInstrumentTerminating);
    }

    private void OnInstrumentTerminating(EntityUid uid, InstrumentComponent component, ref EntityTerminatingEvent args)
    {
        _playlists.Remove(uid);
    }

    // Creates an empty playlist on first access. Callers mutate the returned instance in place.
    public PlaylistData GetPlaylist(EntityUid instrument)
    {
        if (!_playlists.TryGetValue(instrument, out var data))
        {
            data = new PlaylistData();
            _playlists[instrument] = data;
        }
        return data;
    }

    public void NotifyChanged(EntityUid instrument)
    {
        PlaylistChanged?.Invoke(instrument);
    }
}

public sealed class PlaylistData
{
    public readonly List<PlaylistTrack> Tracks = new();

    public int CurrentIndex;

    /// <summary>
    ///     True while a track from this playlist is playing. Cleared on manual Stop or direct File-button open.
    /// </summary>
    public bool Active;
}

public sealed class PlaylistTrack(byte[] bytes, string name)
{
    public byte[] Bytes = bytes;
    // Empty string means "use the default numbered label".
    public string Name = name;
}
