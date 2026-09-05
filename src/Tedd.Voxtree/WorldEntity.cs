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

/// <summary>Identifies a base or level-of-detail chunk in an unbounded world.</summary>
public readonly struct ChunkAddress : IEquatable<ChunkAddress>
{
    /// <summary>Creates an address from an LOD level and its chunk coordinate.</summary>
    public ChunkAddress(int lodLevel, ChunkCoordinate coordinate)
    {
        if (lodLevel < 0) throw new ArgumentOutOfRangeException(nameof(lodLevel));
        LodLevel = lodLevel;
        Coordinate = coordinate;
    }

    /// <summary>Creates an address from an LOD level and chunk-coordinate components.</summary>
    public ChunkAddress(int lodLevel, long x, long y, long z) :
        this(lodLevel, new ChunkCoordinate(x, y, z)) { }

    /// <summary>Zero-based LOD level. Level zero is the base chunk layer.</summary>
    public int LodLevel { get; }
    /// <summary>The coordinate within this LOD layer.</summary>
    public ChunkCoordinate Coordinate { get; }
    /// <summary>The chunk X coordinate.</summary>
    public long X => Coordinate.X;
    /// <summary>The chunk Y coordinate.</summary>
    public long Y => Coordinate.Y;
    /// <summary>The chunk Z coordinate.</summary>
    public long Z => Coordinate.Z;

    /// <inheritdoc />
    public bool Equals(ChunkAddress other) =>
        LodLevel == other.LodLevel && Coordinate == other.Coordinate;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ChunkAddress other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode()
    {
        var coordinateHash = Coordinate.GetHashCode();
        return LodLevel == 0 ? coordinateHash : HashCode.Combine(coordinateHash, LodLevel);
    }
    /// <inheritdoc />
    public override string ToString() => $"LOD {LodLevel} {Coordinate}";

    /// <summary>Tests two addresses for equality.</summary>
    public static bool operator ==(ChunkAddress left, ChunkAddress right) => left.Equals(right);
    /// <summary>Tests two addresses for inequality.</summary>
    public static bool operator !=(ChunkAddress left, ChunkAddress right) => !left.Equals(right);
}

/// <summary>Maps immutable chunks into an effectively unbounded, signed global voxel coordinate system.</summary>
/// <remarks>
/// Chunk coordinates address entries in <see cref="Chunks"/>; voxel coordinates passed to
/// <see cref="Get"/> and <see cref="TryGet"/> are global. Chunk side length must be a power of two,
/// so each global coordinate is routed with an arithmetic shift and a bit mask. This type is not
/// thread-safe; synchronize access when chunks can be changed concurrently with reads.
/// </remarks>
public sealed partial class WorldEntity
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
    /// <summary>The number of loaded base-layer chunks.</summary>
    public int ChunkCount => _chunks.BaseCount;
    /// <summary>The number of loaded chunks across every LOD layer.</summary>
    public int ResidentChunkCount => _chunks.Count;
    /// <summary>A read-only view of the base-layer chunk dictionary.</summary>
    public IReadOnlyDictionary<ChunkCoordinate, OctreeChunk> Chunks => _chunks.Items;
    /// <summary>A read-only view of resident chunks across every LOD layer.</summary>
    public IReadOnlyDictionary<ChunkAddress, OctreeChunk> ResidentChunks => _chunks.AllItems;
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
    public void SetChunk(long chunkX, long chunkY, long chunkZ, OctreeChunk chunk) =>
        SetChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), chunk);

    /// <summary>Adds or replaces a chunk at a chunk coordinate.</summary>
    public void SetChunk(ChunkCoordinate coordinate, OctreeChunk chunk)
        => SetChunk(new ChunkAddress(0, coordinate), chunk);

    /// <summary>Adds or replaces a base or LOD chunk and marks it dirty.</summary>
    public void SetChunk(ChunkAddress address, OctreeChunk chunk)
    {
        ValidateChunkAddress(address);
        if (chunk is null) throw new ArgumentNullException(nameof(chunk));
        if (chunk.SideLength != ChunkSize || chunk.ChannelCount != ChannelCount)
            throw new ArgumentException("Chunk schema does not match the world entity.", nameof(chunk));
        _chunks.Set(address, chunk, chunk.SerializedLength, isDirty: true);
    }


    /// <summary>Adds or replaces an LOD chunk and marks it dirty.</summary>
    public void SetLodChunk(int lodLevel, long chunkX, long chunkY, long chunkZ, OctreeChunk chunk) =>
        SetChunk(new ChunkAddress(lodLevel, chunkX, chunkY, chunkZ), chunk);

    /// <summary>Gets a loaded chunk by chunk coordinate.</summary>
    public bool TryGetChunk(long chunkX, long chunkY, long chunkZ, out OctreeChunk? chunk) =>
        TryGetChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), out chunk);

    /// <summary>Gets a loaded chunk by chunk coordinate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetChunk(ChunkCoordinate coordinate, out OctreeChunk? chunk) =>
        _chunks.TryGetValue(coordinate, out chunk);

    /// <summary>Gets a resident base or LOD chunk without performing storage I/O.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetChunk(ChunkAddress address, out OctreeChunk? chunk)
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
    public bool TryGet(int channel, long x, long y, long z, out uint value)
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
    public bool TryGetLod(int lodLevel, int channel, long x, long y, long z, out uint value)
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
    public uint Get(int channel, long x, long y, long z)
    {
        if (TryGet(channel, x, y, z, out var value)) return value;
        throw new InvalidOperationException("The chunk containing the queried voxel is not loaded.");
    }

    /// <summary>Reads an LOD sample at global base-voxel coordinates.</summary>
    public uint GetLod(int lodLevel, int channel, long x, long y, long z)
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

    private readonly Dictionary<ChunkAddress, TChunk> _items = new();
    private readonly Dictionary<ChunkCoordinate, TChunk> _baseItems = new();
    private readonly Dictionary<ChunkAddress, ResidencyState> _residency = new();
    private readonly ReadOnlyDictionary<ChunkCoordinate, TChunk> _readOnlyItems;
    private readonly ReadOnlyDictionary<ChunkAddress, TChunk> _readOnlyAllItems;
    private readonly CacheEntry[] _recent;
    private readonly int[] _lookup = new int[LookupSize];
    private int _recentCount;
    private ulong _access;
    private long _estimatedBytes;

    internal RecentChunkMap(int recentCapacity)
    {
        _recent = new CacheEntry[recentCapacity];
        _readOnlyItems = new ReadOnlyDictionary<ChunkCoordinate, TChunk>(_baseItems);
        _readOnlyAllItems = new ReadOnlyDictionary<ChunkAddress, TChunk>(_items);
    }

    internal int Count => _items.Count;
    internal int BaseCount => _baseItems.Count;
    internal long EstimatedBytes => _estimatedBytes;
    internal IReadOnlyDictionary<ChunkCoordinate, TChunk> Items => _readOnlyItems;
    internal IReadOnlyDictionary<ChunkAddress, TChunk> AllItems => _readOnlyAllItems;
    internal IEnumerable<KeyValuePair<ChunkAddress, TChunk>> Entries => _items;

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
            if (entry.Hash == hash && entry.LodLevel == 0 && entry.Coordinate == coordinate)
            {
                entry.State!.LastAccessSequence = entry.Access = ++_access;
                value = entry.Value;
                return true;
            }
            slot = (slot + 1) & LookupMask;
        }

        if (_baseItems.TryGetValue(coordinate, out var found))
        {
            var address = new ChunkAddress(0, coordinate);
            var state = _residency[address];
            state.LastAccessSequence = ++_access;
            AddRecent(address, hash, found, state);
            value = found;
            return true;
        }
        value = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetValue(ChunkAddress address, out TChunk? value)
    {
        var hash = address.GetHashCode() & int.MaxValue;
        var slot = hash & LookupMask;
        for (var probe = 0; probe < LookupSize; probe++)
        {
            var recentIndex = _lookup[slot] - 1;
            if (recentIndex < 0) break;
            ref var entry = ref _recent[recentIndex];
            if (entry.Hash == hash && entry.LodLevel == address.LodLevel &&
                entry.Coordinate == address.Coordinate)
            {
                entry.State!.LastAccessSequence = entry.Access = ++_access;
                value = entry.Value;
                return true;
            }
            slot = (slot + 1) & LookupMask;
        }

        if (_items.TryGetValue(address, out var found))
        {
            var state = _residency[address];
            state.LastAccessSequence = ++_access;
            AddRecent(address, hash, found, state);
            value = found;
            return true;
        }
        value = null;
        return false;
    }

    internal bool TryPeekValue(ChunkAddress address, out TChunk? value) =>
        _items.TryGetValue(address, out value);

    internal bool TryPeekEntry(ChunkAddress address, out TChunk? value, out bool isDirty)
    {
        if (_items.TryGetValue(address, out value))
        {
            isDirty = _residency[address].IsDirty;
            return true;
        }
        isDirty = false;
        return false;
    }

    internal void Set(ChunkAddress address, TChunk value, long estimatedBytes, bool isDirty,
        bool isImplicitEmpty = false)
    {
        if (estimatedBytes < 0) throw new ArgumentOutOfRangeException(nameof(estimatedBytes));
        var exists = _residency.TryGetValue(address, out var state);
        var nextEstimatedBytes = checked(_estimatedBytes - (state?.EstimatedBytes ?? 0) + estimatedBytes);
        _items[address] = value;
        if (address.LodLevel == 0) _baseItems[address.Coordinate] = value;
        if (!exists)
        {
            state = new ResidencyState();
            _residency.Add(address, state);
        }
        state!.EstimatedBytes = estimatedBytes;
        state.IsDirty = isDirty;
        state.IsImplicitEmpty = isImplicitEmpty;
        state.LastAccessSequence = ++_access;
        _estimatedBytes = nextEstimatedBytes;
        var hash = address.GetHashCode() & int.MaxValue;
        if (TryFindRecent(address, hash, out var index))
        {
            _recent[index].Value = value;
            _recent[index].State = state;
            _recent[index].Access = state.LastAccessSequence;
            return;
        }
        AddRecent(address, hash, value, state);
    }

    internal bool Remove(ChunkAddress address)
    {
        if (!_items.Remove(address)) return false;
        if (address.LodLevel == 0) _baseItems.Remove(address.Coordinate);
        _estimatedBytes -= _residency[address].EstimatedBytes;
        _residency.Remove(address);
        var hash = address.GetHashCode() & int.MaxValue;
        if (!TryFindRecent(address, hash, out var index)) return true;
        _recentCount--;
        if (index != _recentCount) _recent[index] = _recent[_recentCount];
        _recent[_recentCount] = default;
        RebuildLookup();
        return true;
    }

    internal void Clear()
    {
        _items.Clear();
        _baseItems.Clear();
        _residency.Clear();
        Array.Clear(_recent, 0, _recent.Length);
        Array.Clear(_lookup, 0, _lookup.Length);
        _recentCount = 0;
        _access = 0;
        _estimatedBytes = 0;
    }

    internal bool TryGetResidencyInfo(ChunkAddress address, out ChunkResidencyInfo info)
    {
        if (_residency.TryGetValue(address, out var state))
        {
            info = new ChunkResidencyInfo(address, state.EstimatedBytes,
                state.LastAccessSequence, state.IsDirty, state.IsImplicitEmpty);
            return true;
        }
        info = default;
        return false;
    }

    internal List<EvictionCandidate> GetEvictionCandidates(ChunkAddress? excluded)
    {
        var candidates = new List<EvictionCandidate>(_residency.Count);
        foreach (var pair in _residency)
        {
            if (excluded.HasValue && pair.Key == excluded.Value) continue;
            candidates.Add(new EvictionCandidate(pair.Key, _items[pair.Key],
                pair.Value.IsDirty, pair.Value.LastAccessSequence));
        }
        candidates.Sort(static (left, right) => left.LastAccessSequence.CompareTo(right.LastAccessSequence));
        return candidates;
    }

    internal void MarkClean(ChunkAddress address, TChunk expected)
    {
        if (_items.TryGetValue(address, out var current) && ReferenceEquals(current, expected))
        {
            _residency[address].IsDirty = false;
            _residency[address].IsImplicitEmpty = false;
        }
    }

    internal void MarkAllDirty()
    {
        foreach (var state in _residency.Values)
            if (!state.IsImplicitEmpty) state.IsDirty = true;
    }

    private bool TryFindRecent(ChunkAddress address, int hash, out int index)
    {
        var slot = hash & LookupMask;
        for (var probe = 0; probe < LookupSize; probe++)
        {
            index = _lookup[slot] - 1;
            if (index < 0) return false;
            ref var entry = ref _recent[index];
            if (entry.Hash == hash && entry.LodLevel == address.LodLevel &&
                entry.Coordinate == address.Coordinate) return true;
            slot = (slot + 1) & LookupMask;
        }
        index = -1;
        return false;
    }

    private void AddRecent(ChunkAddress address, int hash, TChunk value, ResidencyState state)
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
        _recent[index] = new CacheEntry(address, hash, value, state);
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
        internal CacheEntry(ChunkAddress address, int hash, TChunk value, ResidencyState state)
        {
            Coordinate = address.Coordinate;
            LodLevel = address.LodLevel;
            Hash = hash;
            Value = value;
            State = state;
            Access = state.LastAccessSequence;
        }

        internal ChunkCoordinate Coordinate;
        internal int LodLevel;
        internal int Hash;
        internal TChunk? Value;
        internal ResidencyState? State;
        internal ulong Access;
    }

    private sealed class ResidencyState
    {
        internal long EstimatedBytes;
        internal ulong LastAccessSequence;
        internal bool IsDirty;
        internal bool IsImplicitEmpty;
    }

    internal readonly struct EvictionCandidate
    {
        internal EvictionCandidate(ChunkAddress address, TChunk value,
            bool isDirty, ulong lastAccessSequence)
        {
            Address = address;
            Value = value;
            IsDirty = isDirty;
            LastAccessSequence = lastAccessSequence;
        }

        internal ChunkAddress Address { get; }
        internal TChunk Value { get; }
        internal bool IsDirty { get; }
        internal ulong LastAccessSequence { get; }
    }
}
