using System.Collections.Concurrent;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._WF.Weather;

/// <summary>
/// For every tile of this grid, whether it is open to the outside and whether it is rooved.
/// </summary>
[RegisterComponent, UnsavedComponent, NetworkedComponent]
public sealed partial class WFExposureComponent : Component
{
    // Without this a grid's tiles go to every player on the server, not just the ones on its map.
    public override bool SessionSpecific => true;

    public static readonly Vector2i[] Cardinals =
        { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    // Eight by eight is sixty-four tiles, which is as many as one number can hold.
    public const byte ChunkSize = 8;

    public static (Vector2i Chunk, ulong Bit) GetChunkBit(Vector2i pos)
    {
        var chunk = SharedMapSystem.GetChunkIndices(pos, ChunkSize);
        return (chunk, SharedMapSystem.ToBitmask(SharedMapSystem.GetChunkRelative(pos, ChunkSize), ChunkSize));
    }

    public static bool HasBit(Dictionary<Vector2i, ulong> masks, Vector2i pos)
    {
        var (chunk, bit) = GetChunkBit(pos);
        return masks.TryGetValue(chunk, out var mask) && (mask & bit) != 0;
    }

    /// <summary>
    /// Answers false if the tile was already marked, which is how a search knows not to visit it twice.
    /// </summary>
    public static bool SetBit(Dictionary<Vector2i, ulong> masks, Vector2i pos)
    {
        var (chunk, bit) = GetChunkBit(pos);
        masks.TryGetValue(chunk, out var mask);

        if ((mask & bit) != 0)
            return false;

        masks[chunk] = mask | bit;
        return true;
    }

    public Dictionary<Vector2i, WFExposureChunk> Chunks = new();

    // The tick each chunk last changed on, and a chunk missing from it never reaches a player.
    public readonly Dictionary<Vector2i, GameTick> LastUpdate = new();

    // The same changes in the order they happened, so a player can be sent only what came after their last one.
    public readonly List<(GameTick Tick, Vector2i Chunk)> Log = new();

    // Players further behind than this have to be sent everything, because what they missed has been thrown away.
    public GameTick LastPrune;

    // A deleted grid takes its waiting tile changes with it.
    public readonly List<(Vector2i Pos, WFTileChange Change)> Pending = new();

    // False until this grid has had all of its tiles worked out at least once.
    public bool Counted;

    // The server puts together several players' copies at the same time, so a plain dictionary would come apart.
    private readonly ConcurrentDictionary<ICommonSession, GameTick> _sentTo = new();

    public bool HasCopy(ICommonSession player) => _sentTo.ContainsKey(player);

    public void DropCopy(ICommonSession player) => _sentTo.TryRemove(player, out _);

    // Answers with the tick of the player's first copy rather than this one.
    public GameTick MarkCopySent(ICommonSession player, GameTick now) => _sentTo.GetOrAdd(player, now);

    public bool WeatherReaches(Vector2i pos, WeatherShelter shelter)
    {
        var (chunkIndex, bit) = GetChunkBit(pos);
        if (!Chunks.TryGetValue(chunkIndex, out var chunk))
            return false;

        // A tile can be unrooved and still walled in, so particulate weather needs it open to the outside as well.
        var mask = shelter == WeatherShelter.Particulate
            ? chunk.OpenToOutside & chunk.OpenOverhead
            : chunk.OpenToOutside;

        return (mask & bit) != 0;
    }

    public bool IsExposed(Vector2i pos) => WeatherReaches(pos, WeatherShelter.Permeating);

    public bool SetOpenToOutside(Vector2i pos, GameTick tick) => Set(pos, tick, open: true, overhead: null);

    public bool SetOpenOverhead(Vector2i pos, GameTick tick) => Set(pos, tick, open: null, overhead: true);

    /// <summary>
    /// Marks a tile as walled off, leaving whether it is rooved alone.
    /// </summary>
    public bool Seal(Vector2i pos, GameTick tick) => Set(pos, tick, open: false, overhead: null);

    /// <summary>
    /// Marks a tile as no longer existing.
    /// </summary>
    public bool Close(Vector2i pos, GameTick tick) => Set(pos, tick, open: false, overhead: false);

    private bool Set(Vector2i pos, GameTick tick, bool? open, bool? overhead)
    {
        var (chunkIndex, bit) = GetChunkBit(pos);
        Chunks.TryGetValue(chunkIndex, out var chunk);
        var updated = chunk;

        if (open is { } openValue)
            updated.OpenToOutside = openValue ? chunk.OpenToOutside | bit : chunk.OpenToOutside & ~bit;

        if (overhead is { } overheadValue)
            updated.OpenOverhead = overheadValue ? chunk.OpenOverhead | bit : chunk.OpenOverhead & ~bit;

        if (updated.OpenToOutside == chunk.OpenToOutside && updated.OpenOverhead == chunk.OpenOverhead)
            return false;

        // Whether a tile is rooved is worth keeping even when it is not open to the outside.
        if (updated.OpenToOutside == 0 && updated.OpenOverhead == 0)
            Chunks.Remove(chunkIndex);
        else
            Chunks[chunkIndex] = updated;

        Stamp(chunkIndex, tick);
        return true;
    }

    public void Stamp(Vector2i chunk, GameTick tick)
    {
        if (LastUpdate.TryGetValue(chunk, out var last) && last == tick)
            return;

        LastUpdate[chunk] = tick;
        Log.Add((tick, chunk));
    }

    /// <summary>
    /// Throws away changes older than the given tick.
    /// </summary>
    public void PruneLog(GameTick before)
    {
        var cut = 0;
        while (cut < Log.Count && Log[cut].Tick < before)
        {
            var (tick, chunk) = Log[cut++];

            // A chunk changed again later is not forgotten, because that later change is still on the list.
            if (!LastUpdate.TryGetValue(chunk, out var last) || last != tick)
                continue;

            LastUpdate.Remove(chunk);
            if (tick > LastPrune)
                LastPrune = tick;
        }

        Log.RemoveRange(0, cut);
    }
}

public struct WFExposureChunk
{
    // Tiles open to the outside, with nothing blocking the way.
    public ulong OpenToOutside;

    // Tiles that are not rooved.
    public ulong OpenOverhead;
}

public enum WFTileChange : byte
{
    // Ground appeared where there was open space.
    Created,

    // Ground stopped existing.
    Removed,

    // Something that stops weather was taken off the tile.
    Unblocked,

    // Something that stops weather was put on the tile.
    Blocked,
}

[Serializable, NetSerializable]
public sealed class WFExposureState(Dictionary<Vector2i, ulong> open, Dictionary<Vector2i, ulong> covered)
    : ComponentState
{
    public Dictionary<Vector2i, ulong> Open = open;

    // Rooved tiles are sent instead, because there are far fewer of those.
    public Dictionary<Vector2i, ulong> Covered = covered;
}

[Serializable, NetSerializable]
public sealed class WFExposureDeltaState(Dictionary<Vector2i, ulong> open, Dictionary<Vector2i, ulong> covered)
    : ComponentState, IComponentDeltaState<WFExposureState>
{
    public Dictionary<Vector2i, ulong> Open = open;
    public Dictionary<Vector2i, ulong> Covered = covered;

    public void ApplyToFullState(WFExposureState state)
    {
        // Zero means the chunk is gone, because a chunk where no tile can have weather is never sent.
        foreach (var (chunk, open) in Open)
        {
            if (open == 0)
            {
                state.Open.Remove(chunk);
                state.Covered.Remove(chunk);
                continue;
            }

            state.Open[chunk] = open;

            if (Covered.TryGetValue(chunk, out var covered))
                state.Covered[chunk] = covered;
            else
                state.Covered.Remove(chunk);
        }
    }

    public WFExposureState CreateNewFullState(WFExposureState state)
    {
        var newState = new WFExposureState(new(state.Open), new(state.Covered));
        ApplyToFullState(newState);
        return newState;
    }
}
