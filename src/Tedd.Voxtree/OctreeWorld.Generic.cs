using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Tedd.Voxtree;

/// <summary>A stored generic world region.</summary>
public readonly struct OctreeWorldRegion<T> where T : unmanaged
{
    /// <summary>Creates an aligned region descriptor.</summary>
    public OctreeWorldRegion(int x, int y, int z, int levels, OctreeRegionKind kind,
        OctreeChunk<T>? chunk = null)
    {
        VoxelType<T>.Validate();
        if ((uint)levels > OctreeWorld<T>.MaxLevels) throw new ArgumentOutOfRangeException(nameof(levels));
        var side = 1 << levels;
        if (x < 0 || y < 0 || z < 0 || x > (1 << OctreeWorld<T>.MaxLevels) - side ||
            y > (1 << OctreeWorld<T>.MaxLevels) - side || z > (1 << OctreeWorld<T>.MaxLevels) - side ||
            ((x | y | z) & (side - 1)) != 0) throw new ArgumentException("Invalid aligned region bounds.");
        if (kind < OctreeRegionKind.Unloaded || kind > OctreeRegionKind.Loaded ||
            (kind == OctreeRegionKind.Loaded ? chunk is null || chunk.Levels != levels : chunk is not null))
            throw new ArgumentException("Region state and payload do not agree.");
        X = x;
        Y = y;
        Z = z;
        Levels = levels;
        Kind = kind;
        Chunk = chunk;
    }

    /// <summary>The minimum voxel X coordinate.</summary>
    public int X { get; }
    /// <summary>The minimum voxel Y coordinate.</summary>
    public int Y { get; }
    /// <summary>The minimum voxel Z coordinate.</summary>
    public int Z { get; }
    /// <summary>The region depth.</summary>
    public int Levels { get; }
    /// <summary>The knowledge/residency state.</summary>
    public OctreeRegionKind Kind { get; }
    /// <summary>The resident chunk, if loaded.</summary>
    public OctreeChunk<T>? Chunk { get; }
    /// <summary>The half-open voxel bounds.</summary>
    public VoxelBox Bounds => new(X, Y, Z, X + (1 << Levels), Y + (1 << Levels), Z + (1 << Levels));
}

/// <summary>A sparse, fixed-capacity streamed world over generic voxel chunks.</summary>
public sealed partial class OctreeWorld<T> : IDisposable where T : unmanaged
{
    /// <summary>Maximum world depth.</summary>
    public const int MaxLevels = OctreeWorld.MaxLevels;

    private readonly Memory<int> _nodes;
    private readonly Memory<int> _slotNext;
    private readonly Memory<OctreeChunk<T>?> _chunks;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private int _root, _nextNode, _freeBranch, _freeBranchCount, _freeSlot;
    private int _residentChunkCount, _branchCount;
    private ulong _revision;

    /// <summary>Provisions a fixed-capacity world.</summary>
    public OctreeWorld(int levels, int chunkLevels, int channelCount, int chunkCapacity,
        int branchCapacity = -1, bool initiallyEmpty = false)
        : this(levels, chunkLevels, channelCount,
            new int[GetRequiredWorkspaceLength(
                ResolveBranches(levels, chunkLevels, chunkCapacity, branchCapacity), chunkCapacity)],
            new OctreeChunk<T>?[chunkCapacity],
            ResolveBranches(levels, chunkLevels, chunkCapacity, branchCapacity), initiallyEmpty) { }

    /// <summary>Uses caller-owned workspace and chunk-reference storage.</summary>
    public OctreeWorld(int levels, int chunkLevels, int channelCount, Memory<int> workspace,
        Memory<OctreeChunk<T>?> chunks, int branchCapacity, bool initiallyEmpty = false)
    {
        VoxelType<T>.Validate();
        ValidateLevels(levels, chunkLevels);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        var required = GetRequiredWorkspaceLength(branchCapacity, chunks.Length);
        if (workspace.Length < required) throw new ArgumentException("Workspace is too short.", nameof(workspace));
        Levels = levels;
        ChunkLevels = chunkLevels;
        ChannelCount = channelCount;
        _nodes = workspace[..(branchCapacity * 8)];
        _slotNext = workspace.Slice(branchCapacity * 8, chunks.Length);
        _chunks = chunks;
        _nodes.Span.Clear();
        _chunks.Span.Clear();
        for (var index = 0; index < chunks.Length; index++) _slotNext.Span[index] = index + 1;
        _root = initiallyEmpty ? -1 : 0;
    }

    private static void ValidateLevels(int levels, int chunkLevels)
    {
        OctreeCodec.ValidateLevels(chunkLevels);
        if (levels < chunkLevels || levels > MaxLevels) throw new ArgumentOutOfRangeException(nameof(levels));
    }

    private static int ResolveBranches(int levels, int chunkLevels, int chunks, int branches)
    {
        ValidateLevels(levels, chunkLevels);
        if (chunks < 0) throw new ArgumentOutOfRangeException(nameof(chunks));
        if (branches < -1) throw new ArgumentOutOfRangeException(nameof(branches));
        return branches == -1 ? checked((levels - chunkLevels) * chunks) : branches;
    }

    /// <summary>Calculates caller workspace capacity.</summary>
    public static int GetRequiredWorkspaceLength(int branchCapacity, int chunkCapacity)
    {
        if (branchCapacity < 0 || chunkCapacity < 0) throw new ArgumentOutOfRangeException(nameof(branchCapacity));
        return checked(branchCapacity * 8 + chunkCapacity);
    }

    /// <summary>The outer world depth.</summary>
    public int Levels { get; }
    /// <summary>The world side length.</summary>
    public int SideLength => 1 << Levels;
    /// <summary>The total logical voxel count.</summary>
    public long VoxelCount => 1L << (3 * Levels);
    /// <summary>The resident chunk depth.</summary>
    public int ChunkLevels { get; }
    /// <summary>The resident chunk side length.</summary>
    public int ChunkSideLength => 1 << ChunkLevels;
    /// <summary>The channel count.</summary>
    public int ChannelCount { get; }
    /// <summary>The resident chunk capacity.</summary>
    public int ChunkCapacity => _chunks.Length;
    /// <summary>The outer branch capacity.</summary>
    public int BranchCapacity => _nodes.Length / 8;
    /// <summary>The resident nonempty chunk count.</summary>
    public int ResidentChunkCount { get { using var scope = ReadLock(); return _residentChunkCount; } }
    /// <summary>The live branch count.</summary>
    public int BranchCount { get { using var scope = ReadLock(); return _branchCount; } }
    /// <summary>The mutation revision.</summary>
    public ulong Revision { get { using var scope = ReadLock(); return _revision; } }
    private int AvailableBranches => (_nodes.Length - _nextNode) / 8 + _freeBranchCount;

    private void ValidatePoint(int x, int y, int z)
    {
        if ((uint)x >= (uint)SideLength || (uint)y >= (uint)SideLength || (uint)z >= (uint)SideLength)
            throw new ArgumentOutOfRangeException(nameof(x), "Voxel coordinates must be inside the world.");
    }

    private void ValidateChannel(int channel)
    {
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
    }

    private void ValidateRegion(int x, int y, int z, int levels)
    {
        if (levels < ChunkLevels || levels > Levels) throw new ArgumentOutOfRangeException(nameof(levels));
        ValidatePoint(x, y, z);
        if (((x | y | z) & ((1 << levels) - 1)) != 0)
            throw new ArgumentException("Region origins must be aligned to their side length.");
    }

    private void ValidateBox(VoxelBox box)
    {
        if (box.MinX < 0 || box.MinY < 0 || box.MinZ < 0 ||
            box.MaxX > SideLength || box.MaxY > SideLength || box.MaxZ > SideLength)
            throw new ArgumentOutOfRangeException(nameof(box));
    }

    private void ChunkOrigin(int cx, int cy, int cz, out int x, out int y, out int z)
    {
        var count = 1 << (Levels - ChunkLevels);
        if ((uint)cx >= (uint)count || (uint)cy >= (uint)count || (uint)cz >= (uint)count)
            throw new ArgumentOutOfRangeException(nameof(cx), "Chunk coordinates must be inside the world.");
        x = cx << ChunkLevels;
        y = cy << ChunkLevels;
        z = cz << ChunkLevels;
    }

    private static int Child(int x, int y, int z, int bit) =>
        (((x >> bit) & 1) << 2) | (((y >> bit) & 1) << 1) | ((z >> bit) & 1);

    private int Find(int x, int y, int z, out int levels)
    {
        var link = _root;
        levels = Levels;
        while (link > 0)
        {
            levels--;
            link = _nodes.Span[link - 1 + Child(x, y, z, levels)];
        }
        return link;
    }

    /// <summary>Gets the stored region containing a voxel.</summary>
    public OctreeWorldRegion<T> GetRegion(int x, int y, int z)
    {
        using var scope = ReadLock();
        ValidatePoint(x, y, z);
        var link = Find(x, y, z, out var levels);
        return Region(link, (x >> levels) << levels, (y >> levels) << levels, (z >> levels) << levels, levels);
    }

    private OctreeWorldRegion<T> Region(int link, int x, int y, int z, int levels) =>
        new(x, y, z, levels,
            link == 0 ? OctreeRegionKind.Unloaded : link == -1 ? OctreeRegionKind.Empty : OctreeRegionKind.Loaded,
            link < -1 ? _chunks.Span[-link - 2] : null);

    /// <summary>Gets a resident chunk by chunk coordinates.</summary>
    public bool TryGetChunk(int chunkX, int chunkY, int chunkZ, out OctreeChunk<T>? chunk)
    {
        using var scope = ReadLock();
        ChunkOrigin(chunkX, chunkY, chunkZ, out var x, out var y, out var z);
        var link = Find(x, y, z, out _);
        chunk = link < -1 ? _chunks.Span[-link - 2] : null;
        return chunk is not null;
    }

    /// <summary>Loads or replaces one chunk without allocating.</summary>
    public bool TryLoadChunk(int chunkX, int chunkY, int chunkZ, OctreeChunk<T> chunk)
    {
        using var scope = WriteLock();
        if (chunk is null) throw new ArgumentNullException(nameof(chunk));
        if (chunk.Levels != ChunkLevels || chunk.ChannelCount != ChannelCount)
            throw new ArgumentException("Chunk schema does not match the world.", nameof(chunk));
        ChunkOrigin(chunkX, chunkY, chunkZ, out var x, out var y, out var z);
        if (chunk.IsEmpty) return TrySetRegion(x, y, z, ChunkLevels, -1);
        var old = Find(x, y, z, out _);
        if (old < -1)
        {
            if (!ReferenceEquals(_chunks.Span[-old - 2], chunk))
            {
                _chunks.Span[-old - 2] = chunk;
                _revision++;
            }
            return true;
        }
        if (_freeSlot == _chunks.Length || !CanSet(x, y, z, ChunkLevels, -2)) return false;
        var slot = _freeSlot;
        _freeSlot = _slotNext.Span[slot];
        _chunks.Span[slot] = chunk;
        _residentChunkCount++;
        _root = Replace(_root, Levels, x, y, z, ChunkLevels, -slot - 2);
        _revision++;
        return true;
    }

    /// <summary>Loads a chunk, throwing if capacity is exhausted.</summary>
    public void LoadChunk(int chunkX, int chunkY, int chunkZ, OctreeChunk<T> chunk)
    {
        if (!TryLoadChunk(chunkX, chunkY, chunkZ, chunk))
            throw new InvalidOperationException("World capacity is exhausted.");
    }

    /// <summary>Imports a stored region, optionally translated.</summary>
    public bool TryLoadRegion(OctreeWorldRegion<T> region, int offsetX = 0, int offsetY = 0, int offsetZ = 0)
    {
        var x = checked(region.X + offsetX);
        var y = checked(region.Y + offsetY);
        var z = checked(region.Z + offsetZ);
        ValidateRegion(x, y, z, region.Levels);
        if (region.Kind == OctreeRegionKind.Empty) return TrySetEmptyRegion(x, y, z, region.Levels);
        if (region.Kind == OctreeRegionKind.Unloaded) return TryUnloadRegion(x, y, z, region.Levels);
        if (region.Levels != ChunkLevels || region.Chunk is null)
            throw new ArgumentException("Invalid loaded region.", nameof(region));
        return TryLoadChunk(x >> ChunkLevels, y >> ChunkLevels, z >> ChunkLevels, region.Chunk);
    }

    /// <summary>Marks an aligned region as known all-zero.</summary>
    public bool TrySetEmptyRegion(int x, int y, int z, int levels) => TrySetRegion(x, y, z, levels, -1);
    /// <summary>Marks an aligned region as unloaded.</summary>
    public bool TryUnloadRegion(int x, int y, int z, int levels) => TrySetRegion(x, y, z, levels, 0);

    private bool TrySetRegion(int x, int y, int z, int levels, int value)
    {
        using var scope = WriteLock();
        ValidateRegion(x, y, z, levels);
        if (!CanSet(x, y, z, levels, value)) return false;
        var old = Find(x, y, z, out var oldLevels);
        if (oldLevels >= levels && old == value) return true;
        _root = Replace(_root, Levels, x, y, z, levels, value);
        _revision++;
        return true;
    }

    private bool CanSet(int x, int y, int z, int target, int value)
    {
        var link = _root;
        var level = Levels;
        while (level > target && link > 0)
        {
            level--;
            link = _nodes.Span[link - 1 + Child(x, y, z, level)];
        }
        return level == target || link == value || AvailableBranches >= level - target;
    }

    private int AllocateBranch(int fill)
    {
        int link;
        if (_freeBranch != 0)
        {
            link = _freeBranch;
            _freeBranch = _nodes.Span[link - 1];
            _freeBranchCount--;
        }
        else
        {
            link = _nextNode + 1;
            _nextNode += 8;
        }
        _nodes.Span.Slice(link - 1, 8).Fill(fill);
        _branchCount++;
        return link;
    }

    private void ReleaseBranch(int link)
    {
        _nodes.Span.Slice(link - 1, 8).Clear();
        _nodes.Span[link - 1] = _freeBranch;
        _freeBranch = link;
        _freeBranchCount++;
        _branchCount--;
    }

    private void Release(int link)
    {
        if (link > 0)
        {
            for (var index = 0; index < 8; index++) Release(_nodes.Span[link - 1 + index]);
            ReleaseBranch(link);
        }
        else if (link < -1)
        {
            var slot = -link - 2;
            _chunks.Span[slot] = null;
            _slotNext.Span[slot] = _freeSlot;
            _freeSlot = slot;
            _residentChunkCount--;
        }
    }

    private int Replace(int link, int level, int x, int y, int z, int target, int value)
    {
        if (link == value) return link;
        if (level == target)
        {
            Release(link);
            return value;
        }
        if (link <= 0) link = AllocateBranch(link);
        var childIndex = link - 1 + Child(x, y, z, level - 1);
        _nodes.Span[childIndex] = Replace(_nodes.Span[childIndex], level - 1, x, y, z, target, value);
        var first = _nodes.Span[link - 1];
        if (first != 0 && first != -1) return link;
        for (var index = 1; index < 8; index++)
            if (_nodes.Span[link - 1 + index] != first) return link;
        ReleaseBranch(link);
        return first;
    }

    /// <summary>Reads a channel voxel; false means out of bounds or unloaded.</summary>
    public bool TryGet(int channel, int x, int y, int z, out T value)
    {
        using var scope = ReadLock();
        ValidateChannel(channel);
        value = default;
        if ((uint)x >= (uint)SideLength || (uint)y >= (uint)SideLength || (uint)z >= (uint)SideLength)
            return false;
        var link = Find(x, y, z, out _);
        if (link == 0) return false;
        if (link == -1) return true;
        var mask = ChunkSideLength - 1;
        value = _chunks.Span[-link - 2]!.GetChannel(channel).Get(x & mask, y & mask, z & mask);
        return true;
    }

    /// <summary>Reads a channel voxel, throwing for unloaded data.</summary>
    public T Get(int channel, int x, int y, int z)
    {
        ValidatePoint(x, y, z);
        if (TryGet(channel, x, y, z, out var value)) return value;
        throw new InvalidOperationException("The queried voxel is unloaded.");
    }

    /// <summary>Determines whether any voxel matches; false return means the result is inconclusive.</summary>
    public bool TryAny(VoxelBox box, int channel, VoxelFilter<T> filter, out bool any)
    {
        using var scope = ReadLock();
        ValidateBox(box);
        ValidateChannel(channel);
        var state = new WorldQuery { Channel = channel, Filter = filter };
        Visit(_root, Levels, 0, 0, 0, box, ref state);
        any = state.Found;
        return state.Found || !state.Unknown;
    }

    /// <summary>Tests whether any voxel matches, throwing when unloaded data prevents a decision.</summary>
    public bool Any(VoxelBox box, int channel, VoxelFilter<T> filter)
    {
        if (TryAny(box, channel, filter, out var any)) return any;
        throw new InvalidOperationException("Unloaded data prevents an occupancy decision.");
    }

    /// <summary>Returns true only when the complete channel region is known all-zero.</summary>
    public bool IsKnownEmpty(VoxelBox box, int channel = 0) =>
        TryAny(box, channel, VoxelFilter<T>.NonZero, out var any) && !any;

    /// <summary>Counts matching known voxels; false means unloaded cells were excluded.</summary>
    public bool TryCountMatches(VoxelBox box, int channel, VoxelFilter<T> filter, out long count)
    {
        using var scope = ReadLock();
        ValidateBox(box);
        ValidateChannel(channel);
        var state = new WorldQuery { Channel = channel, Filter = filter, Counting = true };
        Visit(_root, Levels, 0, 0, 0, box, ref state);
        count = state.Count;
        return !state.Unknown;
    }

    private struct WorldQuery
    {
        internal int Channel;
        internal VoxelFilter<T> Filter;
        internal bool Counting, Unknown, Found;
        internal long Count;
    }

    private static VoxelBox NodeBox(int x, int y, int z, int level)
    {
        var side = 1 << level;
        return new VoxelBox(x, y, z, x + side, y + side, z + side);
    }

    private void Visit(int link, int level, int x, int y, int z, VoxelBox box, ref WorldQuery state)
    {
        var clipped = box.Intersect(NodeBox(x, y, z, level));
        if (clipped.IsEmpty) return;
        if (link > 0)
        {
            var half = 1 << (level - 1);
            for (var index = 0; index < 8 && (state.Counting || !state.Found); index++)
                Visit(_nodes.Span[link - 1 + index], level - 1, x + (index >> 2) * half,
                    y + ((index >> 1) & 1) * half, z + (index & 1) * half, clipped, ref state);
            return;
        }
        if (link == 0)
        {
            state.Unknown = true;
            return;
        }
        if (link == -1)
        {
            if (state.Filter.Matches(default))
            {
                state.Found = true;
                state.Count += (long)(clipped.MaxX - clipped.MinX) *
                               (clipped.MaxY - clipped.MinY) * (clipped.MaxZ - clipped.MinZ);
            }
            return;
        }
        var channel = _chunks.Span[-link - 2]!.GetChannel(state.Channel);
        var local = Translate(clipped, x, y, z);
        if (state.Counting) state.Count += channel.CountMatches(local, state.Filter);
        else state.Found = channel.Any(local, state.Filter);
    }

    private static VoxelBox Translate(VoxelBox box, int x, int y, int z) =>
        new(box.MinX - x, box.MinY - y, box.MinZ - z,
            box.MaxX - x, box.MaxY - y, box.MaxZ - z);

    /// <summary>Copies stored regions intersecting a box into caller storage.</summary>
    public bool QueryRegions(VoxelBox box, Span<OctreeWorldRegion<T>> destination, out int written)
    {
        using var scope = ReadLock();
        ValidateBox(box);
        written = 0;
        return Collect(_root, Levels, 0, 0, 0, box, destination, ref written);
    }

    private bool Collect(int link, int level, int x, int y, int z, VoxelBox box,
        Span<OctreeWorldRegion<T>> destination, ref int written)
    {
        var clipped = box.Intersect(NodeBox(x, y, z, level));
        if (clipped.IsEmpty) return true;
        if (link <= 0)
        {
            if (written == destination.Length) return false;
            destination[written++] = Region(link, x, y, z, level);
            return true;
        }
        var half = 1 << (level - 1);
        for (var index = 0; index < 8; index++)
            if (!Collect(_nodes.Span[link - 1 + index], level - 1, x + (index >> 2) * half,
                    y + ((index >> 1) & 1) * half, z + (index & 1) * half,
                    clipped, destination, ref written)) return false;
        return true;
    }

    /// <summary>Extracts every channel of a dense cubic block.</summary>
    public void CopyBlockTo(int x, int y, int z, int levels, Span<T> destination,
        DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        using var scope = ReadLock();
        var box = DenseBox(x, y, z, levels, layout);
        var count = OctreeCodec.GetVoxelCount(levels);
        PrepareCopy(box, destination, 0, ChannelCount, checked(count * ChannelCount));
        for (var channel = 0; channel < ChannelCount; channel++)
            CopyChannel(box, channel, destination.Slice(channel * count, count), layout);
    }

    /// <summary>Extracts one selected channel in linear or Morton order.</summary>
    public void CopyChannelBlockTo(int channel, int x, int y, int z, int levels,
        Span<T> destination, DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        using var scope = ReadLock();
        ValidateChannel(channel);
        var box = DenseBox(x, y, z, levels, layout);
        var count = OctreeCodec.GetVoxelCount(levels);
        PrepareCopy(box, destination, channel, 1, count);
        CopyChannel(box, channel, destination[..count], layout);
    }

    /// <summary>Extracts an arbitrary one-channel box into packed linear storage.</summary>
    public void CopyRegionTo(int channel, VoxelBox box, Span<T> destination)
    {
        using var scope = ReadLock();
        ValidateChannel(channel);
        ValidateBox(box);
        PrepareCopy(box, destination, channel, 1, box.Count);
        CopyChannel(box, channel, destination[..box.Count], DenseVoxelLayout.Linear);
    }

    private VoxelBox DenseBox(int x, int y, int z, int levels, DenseVoxelLayout layout)
    {
        OctreeCodec.ValidateLevels(levels);
        DenseVoxel.Validate(layout);
        var side = 1 << levels;
        var box = new VoxelBox(x, y, z, checked(x + side), checked(y + side), checked(z + side));
        ValidateBox(box);
        return box;
    }

    private void PrepareCopy(VoxelBox box, Span<T> destination, int channel, int channels, int count)
    {
        if (destination.Length < count) throw new ArgumentException("Destination is too short.", nameof(destination));
        var bytes = MemoryMarshal.AsBytes(destination[..count]);
        if (bytes.Overlaps(MemoryMarshal.AsBytes(_nodes.Span)) ||
            bytes.Overlaps(MemoryMarshal.AsBytes(_slotNext.Span)))
            throw new ArgumentException("Destination overlaps world workspace.");
        CheckReadable(_root, Levels, 0, 0, 0, box, bytes, channel, channels);
    }

    private void CheckReadable(int link, int level, int x, int y, int z, VoxelBox box,
        ReadOnlySpan<byte> destination, int channel, int channels)
    {
        var clipped = box.Intersect(NodeBox(x, y, z, level));
        if (clipped.IsEmpty || link == -1) return;
        if (link == 0) throw new InvalidOperationException("Cannot extract unloaded voxel data.");
        if (link < -1)
        {
            var chunk = _chunks.Span[-link - 2]!;
            for (var index = channel; index < channel + channels; index++)
                if (destination.Overlaps(chunk.GetChannelData(index).Span))
                    throw new ArgumentException("Destination overlaps a channel encoding.");
            return;
        }
        var half = 1 << (level - 1);
        for (var index = 0; index < 8; index++)
            CheckReadable(_nodes.Span[link - 1 + index], level - 1, x + (index >> 2) * half,
                y + ((index >> 1) & 1) * half, z + (index & 1) * half,
                clipped, destination, channel, channels);
    }

    private void CopyChannel(VoxelBox box, int channel, Span<T> destination, DenseVoxelLayout layout)
    {
        destination.Clear();
        CopyLeaves(_root, Levels, 0, 0, 0, box, channel, destination, layout);
    }

    private void CopyLeaves(int link, int level, int x, int y, int z, VoxelBox box,
        int channel, Span<T> destination, DenseVoxelLayout layout)
    {
        if (link is >= -1 and <= 0) return;
        var clipped = box.Intersect(NodeBox(x, y, z, level));
        if (clipped.IsEmpty) return;
        if (link < -1)
        {
            var state = new GenericOctreeQueries<T>.State(GenericOctreeQueries<T>.QueryKind.Copy,
                Translate(clipped, x, y, z), default)
            {
                Values = destination,
                CopyBounds = Translate(box, x, y, z),
                OutputLayout = layout
            };
            GenericOctreeQueries<T>.Run(_chunks.Span[-link - 2]!.GetChannel(channel), ref state);
            return;
        }
        var half = 1 << (level - 1);
        for (var index = 0; index < 8; index++)
            CopyLeaves(_nodes.Span[link - 1 + index], level - 1, x + (index >> 2) * half,
                y + ((index >> 1) & 1) * half, z + (index & 1) * half,
                box, channel, destination, layout);
    }

    /// <summary>Holds a shared lock across multiple operations.</summary>
    public Batch BeginReadBatch()
    {
        _lock.EnterReadLock();
        return new Batch(_lock, write: false);
    }

    /// <summary>Holds an exclusive lock across multiple operations.</summary>
    public Batch BeginWriteBatch()
    {
        _lock.EnterWriteLock();
        return new Batch(_lock, write: true);
    }

    /// <summary>A stack-only, thread-affine world lock scope.</summary>
    public ref struct Batch
    {
        private ReaderWriterLockSlim? _owner;
        private readonly bool _write;
        private readonly int _threadId, _readDepth, _writeDepth;

        internal Batch(ReaderWriterLockSlim owner, bool write)
        {
            _owner = owner;
            _write = write;
            _threadId = Environment.CurrentManagedThreadId;
            _readDepth = owner.RecursiveReadCount;
            _writeDepth = owner.RecursiveWriteCount;
        }

        /// <summary>Releases this batch's lock.</summary>
        public void Dispose()
        {
            var owner = _owner;
            if (owner is null) return;
            if (Environment.CurrentManagedThreadId != _threadId ||
                owner.RecursiveReadCount != _readDepth || owner.RecursiveWriteCount != _writeDepth)
                throw new SynchronizationLockException(
                    "Dispose batches on their creating thread in reverse acquisition order; do not copy them.");
            if (_write) owner.ExitWriteLock();
            else owner.ExitReadLock();
            _owner = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private OperationLock ReadLock()
    {
        if (_lock.IsReadLockHeld || _lock.IsWriteLockHeld) return default;
        _lock.EnterReadLock();
        return new OperationLock(_lock, write: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private OperationLock WriteLock()
    {
        if (_lock.IsWriteLockHeld) return default;
        _lock.EnterWriteLock();
        return new OperationLock(_lock, write: true);
    }

    private readonly struct OperationLock : IDisposable
    {
        private readonly ReaderWriterLockSlim? _owner;
        private readonly bool _write;
        internal OperationLock(ReaderWriterLockSlim owner, bool write)
        {
            _owner = owner;
            _write = write;
        }
        public void Dispose()
        {
            if (_owner is null) return;
            if (_write) _owner.ExitWriteLock();
            else _owner.ExitReadLock();
        }
    }

    /// <summary>Releases synchronization resources after all users have stopped.</summary>
    public void Dispose() => _lock.Dispose();
}
