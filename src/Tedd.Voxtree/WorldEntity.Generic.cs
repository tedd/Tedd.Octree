using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Tedd.Voxtree;

/// <summary>
/// Maps immutable generic chunks into an effectively unbounded, signed global voxel coordinate system.
/// </summary>
/// <remarks>This type is not thread-safe; synchronize access when chunks can change concurrently with reads.</remarks>
public sealed partial class WorldEntity<T> where T : unmanaged
{
    /// <summary>Number of most-recently-used chunks retained by the lookup cache.</summary>
    public const int RecentChunkCapacity = WorldEntity.RecentChunkCapacity;

    private readonly RecentChunkMap<OctreeChunk<T>> _chunks = new(RecentChunkCapacity);
    private readonly int _chunkMask;

    /// <summary>Creates a global coordinate system for generic chunks of one fixed schema.</summary>
    /// <param name="chunkSize">Chunk side length; it must be a power of two in the range 1..512.</param>
    /// <param name="channelCount">Number of channels required in every chunk.</param>
    public WorldEntity(int chunkSize, int channelCount)
    {
        VoxelType<T>.Validate();
        ChunkShift = WorldEntity.ValidateChunkSize(chunkSize);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        ChunkSize = chunkSize;
        ChannelCount = channelCount;
        _chunkMask = chunkSize - 1;
    }

    /// <summary>The fixed power-of-two chunk side length.</summary>
    public int ChunkSize { get; }
    /// <summary>The base-two logarithm of <see cref="ChunkSize"/> used for global-to-chunk shifts.</summary>
    public int ChunkShift { get; }
    /// <summary>The number of channels required in every chunk.</summary>
    public int ChannelCount { get; }
    /// <summary>The number of loaded chunks.</summary>
    public int ChunkCount => _chunks.Count;
    /// <summary>A read-only view of the chunk dictionary.</summary>
    public IReadOnlyDictionary<ChunkCoordinate, OctreeChunk<T>> Chunks => _chunks.Items;

    /// <summary>Returns the chunk coordinate containing a global voxel coordinate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkCoordinate GetChunkCoordinate(long x, long y, long z) =>
        new(x >> ChunkShift, y >> ChunkShift, z >> ChunkShift);

    /// <summary>Resolves global voxel coordinates into a chunk coordinate and chunk-local coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResolveCoordinates(long x, long y, long z, out ChunkCoordinate chunk,
        out int localX, out int localY, out int localZ)
    {
        chunk = new ChunkCoordinate(x >> ChunkShift, y >> ChunkShift, z >> ChunkShift);
        localX = (int)(x & _chunkMask);
        localY = (int)(y & _chunkMask);
        localZ = (int)(z & _chunkMask);
    }

    /// <summary>Adds or replaces a chunk at a chunk coordinate.</summary>
    public void SetChunk(long chunkX, long chunkY, long chunkZ, OctreeChunk<T> chunk) =>
        SetChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), chunk);

    /// <summary>Adds or replaces a chunk at a chunk coordinate.</summary>
    public void SetChunk(ChunkCoordinate coordinate, OctreeChunk<T> chunk)
    {
        if (chunk is null) throw new ArgumentNullException(nameof(chunk));
        if (chunk.SideLength != ChunkSize || chunk.ChannelCount != ChannelCount)
            throw new ArgumentException("Chunk schema does not match the world entity.", nameof(chunk));
        _chunks.Set(coordinate, chunk);
    }

    /// <summary>Gets a loaded chunk by chunk coordinate.</summary>
    public bool TryGetChunk(long chunkX, long chunkY, long chunkZ, out OctreeChunk<T>? chunk) =>
        TryGetChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), out chunk);

    /// <summary>Gets a loaded chunk by chunk coordinate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetChunk(ChunkCoordinate coordinate, out OctreeChunk<T>? chunk) =>
        _chunks.TryGetValue(coordinate, out chunk);

    /// <summary>Removes a loaded chunk by chunk coordinate.</summary>
    public bool RemoveChunk(long chunkX, long chunkY, long chunkZ) =>
        RemoveChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ));

    /// <summary>Removes a loaded chunk by chunk coordinate.</summary>
    public bool RemoveChunk(ChunkCoordinate coordinate) => _chunks.Remove(coordinate);

    /// <summary>Removes every loaded chunk.</summary>
    public void Clear() => _chunks.Clear();

    /// <summary>Reads a channel at global voxel coordinates; false means its chunk is not loaded.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(int channel, long x, long y, long z, out T value)
    {
        ValidateChannel(channel);
        ResolveCoordinates(x, y, z, out var coordinate, out var localX, out var localY, out var localZ);
        if (_chunks.TryGetValue(coordinate, out var chunk))
        {
            value = chunk!.GetChannel(channel).Get(localX, localY, localZ);
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>Reads a channel at global voxel coordinates, throwing when its chunk is not loaded.</summary>
    public T Get(int channel, long x, long y, long z)
    {
        if (TryGet(channel, x, y, z, out var value)) return value;
        throw new InvalidOperationException("The chunk containing the queried voxel is not loaded.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateChannel(int channel)
    {
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
    }
}
