using Content.Client.Audio;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client._WF.Audio;

/// <summary>
/// System that plays combat music when gunfire is detected near the player.
/// </summary>
public sealed class CombatMusicSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // Internal combat state tracking (not using components)
    private TimeSpan _timeSinceLastShot = TimeSpan.Zero;
    private int _recentShotCount = 0;
    private TimeSpan _lastShotTime = TimeSpan.Zero;
    private bool _musicPlaying = false;
    private EntityUid? _musicStream = null;
    private bool _fadingOut = false;

    /// <summary>
    /// Maximum distance from player to detect gunshots (in tiles).
    /// </summary>
    private const float MaxDetectionDistance = 20f;

    /// <summary>
    /// Number of shots needed within the time window to trigger combat music.
    /// </summary>
    private const int ShotsThreshold = 3;

    /// <summary>
    /// Time window in seconds for counting shots.
    /// </summary>
    private const float ShotCountWindow = 5f;

    /// <summary>
    /// Time in seconds without combat before music fades out.
    /// </summary>
    private const float CombatTimeout = 120f;

    /// <summary>
    /// Time in seconds for the fade out duration.
    /// </summary>
    private const float FadeOutDuration = 5f;

    /// <summary>
    /// Volume for combat music.
    /// </summary>
    private const float MusicVolume = -8f;

    /// <summary>
    /// List of combat music files in the Resources/Audio/_WF/CombatMusic directory.
    /// </summary>
    private readonly List<string> _combatMusicTracks = new()
    {
        "/Audio/_WF/CombatMusic/ancient_battle.ogg",
        "/Audio/_WF/CombatMusic/augmented_battle.ogg",
        "/Audio/_WF/CombatMusic/corporate_battle.ogg",
        "/Audio/_WF/CombatMusic/crystal_battle_remastered.ogg",
        "/Audio/_WF/CombatMusic/distant_lights_battle.ogg",
        "/Audio/_WF/CombatMusic/dubmood_cluster_1.ogg",
        "/Audio/_WF/CombatMusic/field_directive.ogg",
        "/Audio/_WF/CombatMusic/forced_to_the_fringes.ogg",
        "/Audio/_WF/CombatMusic/galactic_battle.ogg",
        "/Audio/_WF/CombatMusic/insurgence_battle.ogg",
        "/Audio/_WF/CombatMusic/morph_battle.ogg",
        "/Audio/_WF/CombatMusic/shell_battle.ogg",
        "/Audio/_WF/CombatMusic/smiling_abyss.ogg",
        "/Audio/_WF/CombatMusic/voyager_battle.ogg"
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        // Reset combat state when player attaches
        ResetCombatState();
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        // Stop music when player detaches
        StopMusic();
    }

    private void ResetCombatState()
    {
        _timeSinceLastShot = TimeSpan.Zero;
        _recentShotCount = 0;
        _musicPlaying = false;
        _fadingOut = false;
        StopMusic();
    }

    private void StopMusic()
    {
        if (_musicStream != null)
        {
            _audio.Stop(_musicStream);
            _musicStream = null;
        }
        _musicPlaying = false;
        _fadingOut = false;
    }

    private void OnGunShot(EntityUid uid, GunComponent gun, ref GunShotEvent args)
    {
        // Get local player entity
        var playerEntity = _playerManager.LocalEntity;
        if (playerEntity == null)
            return;

        // Check if the gunshot is within range of the player
        if (!TryComp<TransformComponent>(args.User, out var shooterXform) ||
            !TryComp<TransformComponent>(playerEntity.Value, out var playerXform))
            return;

        var shooterPos = _transform.GetMapCoordinates(shooterXform);
        var playerPos = _transform.GetMapCoordinates(playerXform);

        // Check if on same map
        if (shooterPos.MapId != playerPos.MapId)
            return;

        var distance = (shooterPos.Position - playerPos.Position).Length();
        if (distance > MaxDetectionDistance)
            return;

        // Register the shot
        var curTime = _timing.CurTime;
        _timeSinceLastShot = TimeSpan.Zero;

        // Count shots within the time window
        if (curTime - _lastShotTime > TimeSpan.FromSeconds(ShotCountWindow))
        {
            _recentShotCount = 1;
        }
        else
        {
            _recentShotCount++;
        }

        _lastShotTime = curTime;

        // Start combat music if threshold reached and not already playing
        if (_recentShotCount >= ShotsThreshold && !_musicPlaying)
        {
            StartCombatMusic();
        }
    }

    private void StartCombatMusic()
    {
        // Pick a random combat music track
        var track = _random.Pick(_combatMusicTracks);
        
        // Create audio params for the music
        var audioParams = AudioParams.Default
            .WithVolume(MusicVolume)
            .WithLoop(true);

        // Play the music globally (not positional)
        var stream = _audio.PlayGlobal(track, Filter.Local(), false, audioParams);
        
        if (stream != null)
        {
            _musicStream = stream.Value.Entity;
            _musicPlaying = true;
            _fadingOut = false;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var playerEntity = _playerManager.LocalEntity;
        if (playerEntity == null)
            return;

        if (!_musicPlaying)
            return;

        // Increment time since last shot
        _timeSinceLastShot += TimeSpan.FromSeconds(frameTime);

        // Check if we should start fading out
        if (_timeSinceLastShot.TotalSeconds >= CombatTimeout && !_fadingOut)
        {
            // Start fading out the music
            if (_musicStream != null && EntityManager.EntityExists(_musicStream.Value))
            {
                _contentAudio.FadeOut(_musicStream, null, FadeOutDuration);
                _fadingOut = true;
                
                // Schedule stopping the music after fade completes
                Timer.Spawn(TimeSpan.FromSeconds(FadeOutDuration), () =>
                {
                    if (_musicStream != null)
                    {
                        _audio.Stop(_musicStream);
                        _musicStream = null;
                    }
                    _musicPlaying = false;
                    _fadingOut = false;
                    _recentShotCount = 0;
                });
            }
        }
    }
}
