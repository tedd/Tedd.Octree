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
    /// <summary>The number of loaded base-layer chunks.</summary>
    public int ChunkCount => _chunks.BaseCount;
    /// <summary>The number of loaded chunks across every LOD layer.</summary>
    public int ResidentChunkCount => _chunks.Count;
    /// <summary>A read-only view of the base-layer chunk dictionary.</summary>
    public IReadOnlyDictionary<ChunkCoordinate, OctreeChunk<T>> Chunks => _chunks.Items;
    /// <summary>A read-only view of resident chunks across every LOD layer.</summary>
    public IReadOnlyDictionary<ChunkAddress, OctreeChunk<T>> ResidentChunks => _chunks.AllItems;
    /// <summary>The largest LOD level whose power-of-two coverage fits in a signed Int64.</summary>
    public int MaxLodLevel => 62 - ChunkShift;

    /// <summary>Returns the chunk coordinate containing a global voxel coordinate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkCoordinate GetChunkCoordinate(long x, long y, long z) =>
        new(x >> ChunkShift, y >> ChunkShift, z >> ChunkShift);

    /// <summary>Returns the LOD chunk containing a global base-voxel coordinate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkAddress GetChunkAddress(int lodLevel, long x, long y, long z)
    {
        var shift = GetLodChunkShift(lodLevel);
        return new ChunkAddress(lodLevel, x >> shift, y >> shift, z >> shift);
    }

    /// <summary>Returns the coordinate shift for an LOD layer.</summary>
    public int GetLodChunkShift(int lodLevel)
    {
        ValidateLodLevel(lodLevel);
        return ChunkShift + lodLevel;
    }

    /// <summary>Returns the side length, in base voxels, covered by an LOD chunk.</summary>
    public long GetLodChunkCoverage(int lodLevel) => 1L << GetLodChunkShift(lodLevel);

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

    /// <summary>Resolves global base-voxel coordinates into an LOD address and sample coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResolveLodCoordinates(int lodLevel, long x, long y, long z,
        out ChunkAddress address, out int localX, out int localY, out int localZ)
    {
        var chunkShift = GetLodChunkShift(lodLevel);
        address = new ChunkAddress(lodLevel, x >> chunkShift, y >> chunkShift, z >> chunkShift);
        localX = (int)((x >> lodLevel) & _chunkMask);
        localY = (int)((y >> lodLevel) & _chunkMask);
        localZ = (int)((z >> lodLevel) & _chunkMask);
    }

    /// <summary>Adds or replaces a chunk at a chunk coordinate.</summary>
    public void SetChunk(long chunkX, long chunkY, long chunkZ, OctreeChunk<T> chunk) =>
        SetChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), chunk);

    /// <summary>Adds or replaces a chunk at a chunk coordinate.</summary>
    public void SetChunk(ChunkCoordinate coordinate, OctreeChunk<T> chunk)
        => SetChunk(new ChunkAddress(0, coordinate), chunk);

    /// <summary>Adds or replaces a base or LOD chunk and marks it dirty.</summary>
    public void SetChunk(ChunkAddress address, OctreeChunk<T> chunk)
    {
        ValidateChunkAddress(address);
        if (chunk is null) throw new ArgumentNullException(nameof(chunk));
        if (chunk.SideLength != ChunkSize || chunk.ChannelCount != ChannelCount)
            throw new ArgumentException("Chunk schema does not match the world entity.", nameof(chunk));
        _chunks.Set(address, chunk, chunk.SerializedLength, isDirty: true);
    }


    /// <summary>Adds or replaces an LOD chunk and marks it dirty.</summary>
    public void SetLodChunk(int lodLevel, long chunkX, long chunkY, long chunkZ, OctreeChunk<T> chunk) =>
        SetChunk(new ChunkAddress(lodLevel, chunkX, chunkY, chunkZ), chunk);

    /// <summary>Gets a loaded chunk by chunk coordinate.</summary>
    public bool TryGetChunk(long chunkX, long chunkY, long chunkZ, out OctreeChunk<T>? chunk) =>
        TryGetChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), out chunk);

    /// <summary>Gets a loaded chunk by chunk coordinate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetChunk(ChunkCoordinate coordinate, out OctreeChunk<T>? chunk) =>
        _chunks.TryGetValue(coordinate, out chunk);

    /// <summary>Gets a resident base or LOD chunk without performing storage I/O.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetChunk(ChunkAddress address, out OctreeChunk<T>? chunk)
    {
        ValidateChunkAddress(address);
        return _chunks.TryGetValue(address, out chunk);
    }

    /// <summary>Flushes a dirty base-layer chunk when storage is configured, then unloads it.</summary>
    public bool RemoveChunk(long chunkX, long chunkY, long chunkZ) =>
        RemoveChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ));

    /// <summary>Flushes a dirty base-layer chunk when storage is configured, then unloads it.</summary>
    public bool RemoveChunk(ChunkCoordinate coordinate) =>
        RemoveChunk(new ChunkAddress(0, coordinate));

    /// <summary>Flushes a dirty base or LOD chunk when storage is configured, then unloads it.</summary>
    public bool RemoveChunk(ChunkAddress address) => UnloadChunkCore(address);

    /// <summary>Equivalent to <see cref="RemoveChunk(ChunkAddress)"/>.</summary>
    public bool UnloadChunk(ChunkAddress address) => RemoveChunk(address);

    /// <summary>Flushes a dirty base-layer chunk when storage is configured, then unloads it.</summary>
    public bool UnloadChunk(ChunkCoordinate coordinate) => RemoveChunk(coordinate);

    /// <summary>Flushes a dirty base-layer chunk when storage is configured, then unloads it.</summary>
    public bool UnloadChunk(long chunkX, long chunkY, long chunkZ) =>
        RemoveChunk(chunkX, chunkY, chunkZ);

    /// <summary>Flushes every dirty chunk when storage is configured, then unloads all chunks.</summary>
    public void Clear()
    {
        if (StorageOptions is not null) SaveAllChunks();
        _chunks.Clear();
    }

    /// <summary>Reads a base sample, loading storage or clean zero data on demand when configured.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(int channel, long x, long y, long z, out T value)
    {
        ValidateChannel(channel);
        ResolveCoordinates(x, y, z, out var coordinate, out var localX, out var localY, out var localZ);
        if (_chunks.TryGetValue(coordinate, out var chunk) ||
            (StorageOptions is not null &&
             TryGetOrLoadChunk(new ChunkAddress(0, coordinate), out chunk)))
        {
            value = chunk!.GetChannel(channel).Get(localX, localY, localZ);
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>Reads an LOD sample at global base-voxel coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetLod(int lodLevel, int channel, long x, long y, long z, out T value)
    {
        ValidateChannel(channel);
        ResolveLodCoordinates(lodLevel, x, y, z, out var address,
            out var localX, out var localY, out var localZ);
        if (_chunks.TryGetValue(address, out var chunk) ||
            (StorageOptions is not null && TryGetOrLoadChunk(address, out chunk)))
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

    /// <summary>Reads an LOD sample at global base-voxel coordinates.</summary>
    public T GetLod(int lodLevel, int channel, long x, long y, long z)
    {
        if (TryGetLod(lodLevel, channel, x, y, z, out var value)) return value;
        throw new InvalidOperationException("The LOD chunk containing the queried voxel is not loaded.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateChannel(int channel)
    {
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateChunkAddress(ChunkAddress address) => ValidateLodLevel(address.LodLevel);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateLodLevel(int lodLevel)
    {
        if ((uint)lodLevel > (uint)MaxLodLevel)
            throw new ArgumentOutOfRangeException(nameof(lodLevel),
                $"LOD level must be in the range 0..{MaxLodLevel}.");
    }
}
