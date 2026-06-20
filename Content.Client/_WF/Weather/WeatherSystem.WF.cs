using System.Numerics;
using Content.Shared.Light.Components;
using Content.Shared.Weather;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using AudioComponent = Robust.Shared.Audio.Components.AudioComponent;

namespace Content.Client.Weather;

public sealed partial class WeatherSystem
{
    // Muffling levels picked by how many tiles the player is from the nearest tile with weather on it.
    private const float WeatherAudioOcclusionSilent = 3f;     // No tile with weather on it within search range.
    private const float WeatherAudioOcclusionInterior = 1.5f; // Too far from the weather to hear it clearly.
    private const float WeatherAudioOcclusionBoundary = 0.7f; // A tile or two in from the weather, muffled against the wall.

    // The muffled-against-the-wall level reaches this many tiles in from the weather.
    private const int WeatherAudioBoundaryDepth = 2;

    // Stop searching past this many tiles. If the nearest tile with weather on it is farther than this, the weather is silent.
    private const int WeatherAudioMaxSearchDepth = 16;

    // How fast the volume catches up as the player moves between more and less sheltered tiles.
    private const float WeatherAudioOcclusionFadeRate = 0.5f;

    private static readonly Vector2i[] Cardinals =
        { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private readonly Queue<(Vector2i indices, int depth)> _audioFrontier = new();
    private readonly HashSet<Vector2i> _audioVisited = new();

    private float _audioOcclusionTarget;
    private float _audioUpdateTimer;
    private const float AudioUpdateInterval = 0.25f;

    private Vector2i _audioLastTile;

    protected override void Run(EntityUid uid, WeatherData weather, WeatherPrototype weatherProto, float frameTime)
    {
        base.Run(uid, weather, weatherProto, frameTime);

        var ent = _playerManager.LocalEntity;

        if (ent == null)
            return;

        var mapUid = Transform(uid).MapUid;
        var entXform = Transform(ent.Value);

        if (mapUid == null || entXform.MapUid != mapUid)
        {
            weather.Stream = _audio.Stop(weather.Stream);
            return;
        }

        if (!Timing.IsFirstTimePredicted || weatherProto.Sound == null)
            return;

        var streamWasNull = weather.Stream == null;
        weather.Stream ??= _audio.PlayGlobal(weatherProto.Sound, Filter.Local(), true)?.Entity;

        if (!TryComp(weather.Stream, out AudioComponent? comp))
            return;

        // A new audio stream starts at full volume. Set it to the player's current muffle level
        // right away, or the weather plays loud for a few seconds before the fade settles.
        if (streamWasNull)
            comp.Occlusion = _audioOcclusionTarget = ComputeOcclusionForLocalPlayer(weatherProto);

        if (TryComp<MapGridComponent>(entXform.GridUid, out var grid))
        {
            var gridUid = entXform.GridUid.Value;
            var tile = _mapSystem.TileIndicesFor(gridUid, grid, entXform.Coordinates);
            _audioUpdateTimer -= frameTime;

            if (tile != _audioLastTile && _audioUpdateTimer <= 0f)
            {
                _audioUpdateTimer = AudioUpdateInterval;
                _audioLastTile = tile;
                TryComp(gridUid, out RoofComponent? roofComp);
                _audioOcclusionTarget = ComputeWeatherOcclusionTier(gridUid, grid, entXform.Coordinates, roofComp, weatherProto);
            }
        }

        var smoothedOcclusion = SmoothWeatherOcclusion(comp.Occlusion, _audioOcclusionTarget, frameTime);

        var alpha = GetPercent(weather, uid);
        alpha *= SharedAudioSystem.VolumeToGain(weatherProto.Sound.Params.Volume);
        alpha *= GainAttenuationFromOcclusion(smoothedOcclusion);

        _audio.SetGain(weather.Stream, alpha, comp);
        comp.Occlusion = smoothedOcclusion;
    }

    protected override bool SetState(EntityUid uid, WeatherState state, WeatherComponent comp, WeatherData weather, WeatherPrototype weatherProto)
    {
        if (!base.SetState(uid, state, comp, weather, weatherProto))
            return false;

        if (!Timing.IsFirstTimePredicted)
            return true;

        weather.Stream = _audio.Stop(weather.Stream);
        weather.Stream = _audio.PlayGlobal(weatherProto.Sound, Filter.Local(), true)?.Entity;
        // Changing the weather restarts the audio. A fresh stream starts at full volume, so set
        // it to the player's current muffle level right away instead of fading down from loud.
        if (TryComp(weather.Stream, out AudioComponent? audio))
            audio.Occlusion = ComputeOcclusionForLocalPlayer(weatherProto);
        return true;
    }

    // Counts the tiles from the player to the nearest tile with weather on it and picks the volume from that distance.
    // Full volume standing in the weather, then three quieter levels further in.
    private float ComputeWeatherOcclusionTier(EntityUid gridId, MapGridComponent grid, EntityCoordinates coordinates, RoofComponent? roofComp, WeatherPrototype proto)
    {
        var seed = _mapSystem.GetTileRef(gridId, grid, coordinates);
        var foundDepth = FindNearestExposedTileDepth(gridId, grid, seed, roofComp, proto);

        if (foundDepth == null)
            return WeatherAudioOcclusionSilent;
        if (foundDepth.Value == 0)
            return 0f;
        if (foundDepth.Value <= WeatherAudioBoundaryDepth)
            return WeatherAudioOcclusionBoundary;
        return WeatherAudioOcclusionInterior;
    }

    private int? FindNearestExposedTileDepth(EntityUid gridId, MapGridComponent grid, TileRef seed, RoofComponent? roofComp, WeatherPrototype proto)
    {
        if (CanWeatherAffect(gridId, grid, seed, proto, roofComp))
            return 0;

        _audioFrontier.Clear();
        _audioVisited.Clear();
        _audioFrontier.Enqueue((seed.GridIndices, 0));
        _audioVisited.Add(seed.GridIndices);

        while (_audioFrontier.TryDequeue(out var entry))
        {
            if (entry.depth >= WeatherAudioMaxSearchDepth)
                continue;

            foreach (var off in Cardinals)
            {
                var newIdx = entry.indices + off;
                if (!_audioVisited.Add(newIdx))
                    continue;

                var tile = _mapSystem.GetTileRef(gridId, grid, newIdx);
                if (CanWeatherAffect(gridId, grid, tile, proto, roofComp))
                    return entry.depth + 1;

                _audioFrontier.Enqueue((newIdx, entry.depth + 1));
            }
        }

        return null;
    }

    // Eases the volume between levels so it does not jump as the player moves.
    private float SmoothWeatherOcclusion(float current, float target, float frameTime)
    {
        var fadeFactor = 1f - MathF.Exp(-frameTime * WeatherAudioOcclusionFadeRate);
        return current + (target - current) * fadeFactor;
    }

    // Converts the muffling level into how loud the weather plays. The volume falls off quickly,
    // so standing just inside a window is clearly louder than deep in the room.
    private static float GainAttenuationFromOcclusion(float occlusion)
    {
        var clear = Math.Clamp(1f - occlusion / WeatherAudioOcclusionSilent, 0f, 1f);
        return clear * clear;
    }

    // Muffling level the local player should hear, used to set a new audio stream's
    // starting volume so it does not ramp up from zero.
    private float ComputeOcclusionForLocalPlayer(WeatherPrototype proto)
    {
        var ent = _playerManager.LocalEntity;
        if (ent == null)
            return 0f;
        var entXform = Transform(ent.Value);
        if (!TryComp<MapGridComponent>(entXform.GridUid, out var grid))
            return 0f;
        TryComp(entXform.GridUid, out RoofComponent? roofComp);
        return ComputeWeatherOcclusionTier(entXform.GridUid.Value, grid, entXform.Coordinates, roofComp, proto);
    }
}
