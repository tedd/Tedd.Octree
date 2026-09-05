using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace Tedd.Voxtree;

/// <summary>Identifies a chunk in an unbounded, signed three-dimensional grid.</summary>
public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>
{
    /// <summary>Creates a chunk coordinate.</summary>
    public ChunkCoordinate(long x, long y, long z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>The chunk X coordinate.</summary>
    public long X { get; }
    /// <summary>The chunk Y coordinate.</summary>
    public long Y { get; }
    /// <summary>The chunk Z coordinate.</summary>
    public long Z { get; }

    /// <summary>Deconstructs this coordinate.</summary>
    public void Deconstruct(out long x, out long y, out long z)
    {
        x = X;
        y = Y;
        z = Z;
    }

    /// <inheritdoc />
    public bool Equals(ChunkCoordinate other) => X == other.X && Y == other.Y && Z == other.Z;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ChunkCoordinate other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y}, {Z})";

    /// <summary>Tests two coordinates for equality.</summary>
    public static bool operator ==(ChunkCoordinate left, ChunkCoordinate right) => left.Equals(right);
    /// <summary>Tests two coordinates for inequality.</summary>
    public static bool operator !=(ChunkCoordinate left, ChunkCoordinate right) => !left.Equals(right);
}

/// <summary>Maps immutable chunks into an effectively unbounded, signed global voxel coordinate system.</summary>
/// <remarks>
/// Chunk coordinates address entries in <see cref="Chunks"/>; voxel coordinates passed to
/// <see cref="Get"/> and <see cref="TryGet"/> are global. Chunk side length must be a power of two,
/// so each global coordinate is routed with an arithmetic shift and a bit mask. This type is not
/// thread-safe; synchronize access when chunks can be changed concurrently with reads.
/// </remarks>
public sealed class WorldEntity
{
    /// <summary>Number of most-recently-used chunks retained by the lookup cache.</summary>
    public const int RecentChunkCapacity = 10;

    private readonly RecentChunkMap<OctreeChunk> _chunks = new(RecentChunkCapacity);
    private readonly int _chunkMask;

    /// <summary>Creates a global coordinate system for chunks of one fixed schema.</summary>
    /// <param name="chunkSize">Chunk side length; it must be a power of two in the range 1..512.</param>
    /// <param name="channelCount">Number of channels required in every chunk.</param>
    public WorldEntity(int chunkSize, int channelCount)
    {
        ChunkShift = ValidateChunkSize(chunkSize);
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
    public IReadOnlyDictionary<ChunkCoordinate, OctreeChunk> Chunks => _chunks.Items;

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
    public void SetChunk(long chunkX, long chunkY, long chunkZ, OctreeChunk chunk) =>
        SetChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), chunk);

    /// <summary>Adds or replaces a chunk at a chunk coordinate.</summary>
    public void SetChunk(ChunkCoordinate coordinate, OctreeChunk chunk)
    {
        if (chunk is null) throw new ArgumentNullException(nameof(chunk));
        if (chunk.SideLength != ChunkSize || chunk.ChannelCount != ChannelCount)
            throw new ArgumentException("Chunk schema does not match the world entity.", nameof(chunk));
        _chunks.Set(coordinate, chunk);
    }

    /// <summary>Gets a loaded chunk by chunk coordinate.</summary>
    public bool TryGetChunk(long chunkX, long chunkY, long chunkZ, out OctreeChunk? chunk) =>
        TryGetChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), out chunk);

    /// <summary>Gets a loaded chunk by chunk coordinate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetChunk(ChunkCoordinate coordinate, out OctreeChunk? chunk) =>
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
    public bool TryGet(int channel, long x, long y, long z, out uint value)
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
    public uint Get(int channel, long x, long y, long z)
    {
        if (TryGet(channel, x, y, z, out var value)) return value;
        throw new InvalidOperationException("The chunk containing the queried voxel is not loaded.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateChannel(int channel)
    {
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
    }

    internal static int ValidateChunkSize(int chunkSize)
    {
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0 || chunkSize > (1 << Octree.MaxLevels))
            throw new ArgumentOutOfRangeException(nameof(chunkSize),
                $"Chunk size must be a power of two in the range 1..{1 << Octree.MaxLevels}.");
        var shift = 0;
        while ((1 << shift) != chunkSize) shift++;
        return shift;
    }
}

/// <summary>A dictionary with a small, hash-indexed most-recently-used lookup cache.</summary>
internal sealed class RecentChunkMap<TChunk> where TChunk : class
{
    private const int LookupSize = 16;
    private const int LookupMask = LookupSize - 1;

    private readonly Dictionary<ChunkCoordinate, TChunk> _items = new();
    private readonly ReadOnlyDictionary<ChunkCoordinate, TChunk> _readOnlyItems;
    private readonly CacheEntry[] _recent;
    private readonly int[] _lookup = new int[LookupSize];
    private int _recentCount;
    private ulong _access;

    internal RecentChunkMap(int recentCapacity)
    {
        _recent = new CacheEntry[recentCapacity];
        _readOnlyItems = new ReadOnlyDictionary<ChunkCoordinate, TChunk>(_items);
    }

    internal int Count => _items.Count;
    internal IReadOnlyDictionary<ChunkCoordinate, TChunk> Items => _readOnlyItems;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetValue(ChunkCoordinate coordinate, out TChunk? value)
    {
        var hash = coordinate.GetHashCode() & int.MaxValue;
        var slot = hash & LookupMask;
        for (var probe = 0; probe < LookupSize; probe++)
        {
            var recentIndex = _lookup[slot] - 1;
            if (recentIndex < 0) break;
            ref var entry = ref _recent[recentIndex];
            if (entry.Hash == hash && entry.Coordinate == coordinate)
            {
                entry.Access = ++_access;
                value = entry.Value;
                return true;
            }
            slot = (slot + 1) & LookupMask;
        }

        if (_items.TryGetValue(coordinate, out var found))
        {
            AddRecent(coordinate, hash, found);
            value = found;
            return true;
        }
        value = null;
        return false;
    }

    internal void Set(ChunkCoordinate coordinate, TChunk value)
    {
        _items[coordinate] = value;
        var hash = coordinate.GetHashCode() & int.MaxValue;
        if (TryFindRecent(coordinate, hash, out var index))
        {
            _recent[index].Value = value;
            _recent[index].Access = ++_access;
            return;
        }
        AddRecent(coordinate, hash, value);
    }

    internal bool Remove(ChunkCoordinate coordinate)
    {
        if (!_items.Remove(coordinate)) return false;
        var hash = coordinate.GetHashCode() & int.MaxValue;
        if (!TryFindRecent(coordinate, hash, out var index)) return true;
        _recentCount--;
        if (index != _recentCount) _recent[index] = _recent[_recentCount];
        _recent[_recentCount] = default;
        RebuildLookup();
        return true;
    }

    internal void Clear()
    {
        _items.Clear();
        Array.Clear(_recent, 0, _recent.Length);
        Array.Clear(_lookup, 0, _lookup.Length);
        _recentCount = 0;
        _access = 0;
    }

    private bool TryFindRecent(ChunkCoordinate coordinate, int hash, out int index)
    {
        var slot = hash & LookupMask;
        for (var probe = 0; probe < LookupSize; probe++)
        {
            index = _lookup[slot] - 1;
            if (index < 0) return false;
            ref var entry = ref _recent[index];
            if (entry.Hash == hash && entry.Coordinate == coordinate) return true;
            slot = (slot + 1) & LookupMask;
        }
        index = -1;
        return false;
    }

    private void AddRecent(ChunkCoordinate coordinate, int hash, TChunk value)
    {
        int index;
        if (_recentCount < _recent.Length)
        {
            index = _recentCount++;
        }
        else
        {
            index = 0;
            for (var candidate = 1; candidate < _recentCount; candidate++)
                if (_recent[candidate].Access < _recent[index].Access) index = candidate;
        }
        _recent[index] = new CacheEntry(coordinate, hash, value, ++_access);
        RebuildLookup();
    }

    private void RebuildLookup()
    {
        Array.Clear(_lookup, 0, _lookup.Length);
        for (var index = 0; index < _recentCount; index++)
        {
            var slot = _recent[index].Hash & LookupMask;
            while (_lookup[slot] != 0) slot = (slot + 1) & LookupMask;
            _lookup[slot] = index + 1;
        }
    }

    private struct CacheEntry
    {
        internal CacheEntry(ChunkCoordinate coordinate, int hash, TChunk value, ulong access)
        {
            Coordinate = coordinate;
            Hash = hash;
            Value = value;
            Access = access;
        }

        internal ChunkCoordinate Coordinate;
        internal int Hash;
        internal TChunk? Value;
        internal ulong Access;
    }
}
