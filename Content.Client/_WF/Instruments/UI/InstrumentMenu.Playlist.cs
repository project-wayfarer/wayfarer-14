using Content.Client._WF.Instruments;
using Content.Client._WF.Instruments.UI;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Instruments.UI;

public sealed partial class InstrumentMenu
{
    private PlaylistMenu? _playlistWindow;

    // True while waiting for the server's echoed stop event between playlist tracks.
    // FrameUpdate checks this to avoid resetting the slider and buttons during the gap.
    private bool _playlistAdvancing;

    private void InitializePlaylist()
    {
        PlaylistButton.OnPressed += PlaylistButtonOnPressed;
        PrevTrackButton.OnPressed += _ => Skip(-1);
        NextTrackButton.OnPressed += _ => Skip(1);
    }

    // Dispose the child window so its PlaylistChanged subscription is freed and the
    // OnPlay callback is not left pointing at a closed menu.
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _playlistWindow?.Dispose();
        base.Dispose(disposing);
    }

    // Robust's BaseWindow.Close removes the window from the UI tree without disposing it,
    // so checking Disposed would treat a user-closed window as still open. Use IsOpen.
    private void PlaylistButtonOnPressed(ButtonEventArgs obj)
    {
        if (_playlistWindow is { IsOpen: true })
        {
            _playlistWindow.MoveToFront();
            return;
        }

        _playlistWindow ??= new PlaylistMenu
        {
            Instrument = Entity,
            OnPlay = StartPlaylist,
        };
        _playlistWindow.OpenCentered();
    }

    // The extra + Tracks.Count keeps the result non-negative when delta is -1 and
    // CurrentIndex is 0, before the modulo wraps it to the last track.
    private void Skip(int delta)
    {
        var data = _entManager.System<PlaylistSystem>().GetPlaylist(Entity);
        if (!data.Active || data.Tracks.Count == 0)
            return;

        data.CurrentIndex = (data.CurrentIndex + delta + data.Tracks.Count) % data.Tracks.Count;
        PlayCurrentPlaylistSong();
    }

    private void StartPlaylist(int startIndex)
    {
        var data = _entManager.System<PlaylistSystem>().GetPlaylist(Entity);
        if (data.Tracks.Count == 0)
            return;

        data.CurrentIndex = Math.Clamp(startIndex, 0, data.Tracks.Count - 1);
        data.Active = true;
        PlayCurrentPlaylistSong();
    }

    private void PlayCurrentPlaylistSong()
    {
        var system = _entManager.System<PlaylistSystem>();
        var data = system.GetPlaylist(Entity);

        // Pending advance timer can run after Stop, a direct File open, or a Skip.
        if (!data.Active || data.Tracks.Count == 0)
            return;

        if (!PlayCheck())
        {
            DeactivatePlaylist();
            return;
        }

        if (!_entManager.TryGetComponent<InstrumentComponent>(Entity, out var instrument))
        {
            DeactivatePlaylist();
            return;
        }

        var bytes = data.Tracks[data.CurrentIndex].Bytes;

        if (!_entManager.System<InstrumentSystem>().OpenMidi(Entity, bytes, instrument))
        {
            DeactivatePlaylist();
            return;
        }

        MidiPlaybackSetButtonsDisabled(false);
        if (InputButton.Pressed)
            InputButton.Pressed = false;

        _playlistAdvancing = false;
        system.NotifyChanged(Entity);
    }

    // Returns true if the playlist handled the event, so the upstream handler skips
    // disabling the playback buttons.
    private bool OnPlaylistPlaybackEnded()
    {
        var data = _entManager.System<PlaylistSystem>().GetPlaylist(Entity);
        if (!data.Active || data.Tracks.Count == 0)
            return false;

        if (!LoopButton.Pressed)
            data.CurrentIndex = (data.CurrentIndex + 1) % data.Tracks.Count;

        // Server echoes InstrumentStopMidiEvent to every client at MIDI end (Content.Server
        // InstrumentSystem.Clean). The echo's EndRenderer kills any renderer opened before
        // the echo arrives. 250ms covers a typical RTT.
        _playlistAdvancing = true;
        Timer.Spawn(250, PlayCurrentPlaylistSong);
        return true;
    }

    private void DeactivatePlaylist()
    {
        _playlistAdvancing = false;
        var system = _entManager.System<PlaylistSystem>();
        var data = system.GetPlaylist(Entity);
        if (!data.Active)
            return;
        data.Active = false;
        data.CurrentIndex = 0;
        system.NotifyChanged(Entity);
    }
}
