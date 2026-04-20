using Content.Shared.CCVar;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;

namespace Content.Client.Audio;

public sealed partial class ContentAudioSystem
{
    private const string AndyAnnouncementPathPrefix = "/Audio/_NF/Announcements/PocketSizedAndy/";

    private float _andyAnnouncementVolume;
    private bool _andyAnnouncementsInitialized;

    private void InitializeAndyAnnouncements()
    {
        if (_andyAnnouncementsInitialized)
            return;

        _andyAnnouncementsInitialized = true;
        Subs.CVar(_configManager, CCVars.AndyAnnouncementVolume, AndyAnnouncementVolumeChanged, true);
    }

    private void AndyAnnouncementVolumeChanged(float volume)
    {
        _andyAnnouncementVolume = SharedAudioSystem.GainToVolume(volume);

        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            UpdateAndyAnnouncementVolume(uid, component);
        }
    }

    private void UpdateAndyAnnouncementVolumes()
    {
        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            UpdateAndyAnnouncementVolume(uid, component);
        }
    }

    private void UpdateAndyAnnouncementVolume(EntityUid uid, AudioComponent component)
    {
        if (!IsAndyAnnouncement(component.FileName))
            return;

        var expected = component.Params.Volume + _andyAnnouncementVolume;
        if (MathF.Abs(component.Volume - expected) < 0.001f)
            return;

        component.Volume = expected;
    }

    private static bool IsAndyAnnouncement(string fileName)
    {
        if (!fileName.StartsWith(AndyAnnouncementPathPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var filenameStart = fileName.LastIndexOf('/') + 1;
        if (filenameStart <= 0 || filenameStart >= fileName.Length)
            return false;

        return fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
            && fileName[filenameStart..].StartsWith("andy", StringComparison.OrdinalIgnoreCase);
    }
}