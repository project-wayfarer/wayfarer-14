using Content.Client.Audio;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions;
using Robust.Client.Player;
using Robust.Shared.GameStates;

namespace Content.Client.Salvage;

public sealed class SalvageSystem : SharedSalvageSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ContentAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnPlayAmbientMusic);
        SubscribeLocalEvent<SalvageExpeditionComponent, ComponentHandleState>(OnExpeditionHandleState);
    }

    private void OnExpeditionHandleState(EntityUid uid, SalvageExpeditionComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not SalvageExpeditionComponentState state)
            return;

        component.Stage = state.Stage;
        if (state.SelectedSong != null) // Frontier
            component.SelectedSong = state.SelectedSong; // Frontier

        if (component.Stage >= ExpeditionStage.MusicCountdown)
        {
            _audio.DisableAmbientMusic();
        }

    }

    private void OnPlayAmbientMusic(ref PlayAmbientMusicEvent ev)
    {
        if (ev.Cancelled)
            return;

        var player = _playerManager.LocalEntity;

        if (!TryComp(player, out TransformComponent? xform) ||
            !TryComp<SalvageExpeditionComponent>(xform.MapUid, out var expedition) ||
            expedition.Stage < ExpeditionStage.MusicCountdown)
        {
            return;
        }

        ev.Cancelled = true;
    }
    /* // Wayfarer: Guess we ain't using this?
    private void SetMusicVolume(float volume)
    {
        var expedQuery = EntityQueryEnumerator<SalvageExpeditionComponent>();
        while (expedQuery.MoveNext(out _, out var comp))
        {
            if (comp.Stream != null)
                _audioSystem.SetVolume(comp.Stream, ConvertSliderValueToVolume(volume));
        }
    }

    private float ConvertSliderValueToVolume(float value)
    {
        var ret = AudioSystem.GainToVolume(value);
        if (!float.IsFinite(ret)) // Explicitly handle any odd cases (chiefly NaN)
            ret = SalvageExpeditionMinMusicVolume;
        else
            ret = Math.Clamp(ret, SalvageExpeditionMinMusicVolume, SalvageExpeditionMaxMusicVolume);
        return ret;
    }
    */
    // End Frontier: stop stream when destroying the expedition

    // Frontier: resolve expedition comp
    public override bool ResolveExpedition(EntityUid? uid, ref SharedSalvageExpeditionComponent? component)
    {
        if (component is not null)
            return true;

        TryComp<SalvageExpeditionComponent>(uid, out var localComp);
        component = localComp;
        return component != null;
    }
    // End Frontier
}
