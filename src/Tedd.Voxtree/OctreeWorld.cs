using System;

namespace Tedd.Voxtree;

/// <summary>The knowledge/residency state of a region in a streamed voxel world.</summary>
public enum OctreeRegionKind
{
    /// <summary>Values are unknown because the region is not loaded.</summary>
    Unloaded,
    /// <summary>Every channel is known to contain only zero; no payload is retained.</summary>
    Empty,
    /// <summary>A multi-channel encoded chunk is resident.</summary>
    Loaded
}

/// <summary>A stored world region, suitable for streaming decisions or persistence manifests.</summary>
public readonly struct OctreeWorldRegion
{
    /// <summary>Creates an aligned region descriptor, for example when restoring a persistence manifest.</summary>
    public OctreeWorldRegion(int x, int y, int z, int levels, OctreeRegionKind kind, OctreeChunk? chunk = null)
    {
        if ((uint)levels > OctreeWorld.MaxLevels) throw new ArgumentOutOfRangeException(nameof(levels));
        var side = 1 << levels;
        if (x < 0 || y < 0 || z < 0 || x > (1 << OctreeWorld.MaxLevels) - side ||
            y > (1 << OctreeWorld.MaxLevels) - side || z > (1 << OctreeWorld.MaxLevels) - side ||
            ((x | y | z) & (side - 1)) != 0) throw new ArgumentException("Invalid aligned region bounds.");
        if (kind < OctreeRegionKind.Unloaded || kind > OctreeRegionKind.Loaded ||
            (kind == OctreeRegionKind.Loaded ? chunk is null || chunk.Levels != levels : chunk is not null))
            throw new ArgumentException("Region state and payload do not agree.");
        X = x; Y = y; Z = z; Levels = levels; Kind = kind; Chunk = chunk;
    }
    /// <summary>The region's minimum voxel X coordinate.</summary>
    public int X { get; }
    /// <summary>The region's minimum voxel Y coordinate.</summary>
    public int Y { get; }
    /// <summary>The region's minimum voxel Z coordinate.</summary>
    public int Z { get; }
    /// <summary>The depth of this aligned region.</summary>
    public int Levels { get; }
    /// <summary>The region's knowledge/residency state.</summary>
    public OctreeRegionKind Kind { get; }
    /// <summary>The resident chunk, or null for empty/unloaded regions.</summary>
    public OctreeChunk? Chunk { get; }
    /// <summary>The stored region's half-open bounds.</summary>
    public VoxelBox Bounds => new(X, Y, Z, X + (1 << Levels), Y + (1 << Levels), Z + (1 << Levels));
}

/// <summary>A sparse, fixed-capacity outer octree over independently loadable multi-channel chunks.</summary>
/// <remarks>
/// Empty and unloaded regions collapse independently. Load/unload/query operations allocate no managed
/// memory after provisioning. Not thread-safe; synchronize mutations and do not mutate retained chunk bytes.
/// Outer depth is independent of the bounded dense/encoded chunk depth.
/// </remarks>
public sealed partial class OctreeWorld
{
    /// <summary>Maximum world depth; its cubic voxel count fits in Int64.</summary>
    public const int MaxLevels = 20;
    private readonly Memory<int> _nodes;
    private readonly Memory<int> _slotNext;
    private readonly Memory<OctreeChunk?> _chunks;
    // Links: 0 unknown, -1 empty, -(slot+2) resident, positive = eight-child offset + 1.
    private int _root, _nextNode, _freeBranch, _freeBranchCount, _freeSlot;

    /// <summary>Provisions a world with fixed resident-chunk and branch capacities.</summary>
    /// <param name="levels">World depth, chunkLevels..20. Ten represents a 1024-cubed world.</param>
    /// <param name="chunkLevels">Dense/encoded chunk depth, 0..9. Five represents 32-cubed chunks.</param>
    /// <param name="channelCount">Number of independent channels in each chunk.</param>
    /// <param name="chunkCapacity">Maximum simultaneously resident nonempty chunks.</param>
    /// <param name="branchCapacity">Maximum eight-child branches. -1 reserves a worst-case path per resident chunk.</param>
    /// <param name="initiallyEmpty">True only when the entire world is already known to be zero, not merely unloaded.</param>
    public OctreeWorld(int levels, int chunkLevels, int channelCount, int chunkCapacity,
        int branchCapacity = -1, bool initiallyEmpty = false)
        : this(levels, chunkLevels, channelCount,
            new int[GetRequiredWorkspaceLength(ResolveBranches(levels, chunkLevels, chunkCapacity, branchCapacity), chunkCapacity)],
            new OctreeChunk?[chunkCapacity], ResolveBranches(levels, chunkLevels, chunkCapacity, branchCapacity), initiallyEmpty) { }

    /// <summary>Uses caller-owned workspace and chunk-reference storage, clearing them on construction.</summary>
    /// <remarks>Both memories must remain exclusive and alive. Workspace uses eight integers per branch plus one per chunk slot.</remarks>
    public OctreeWorld(int levels, int chunkLevels, int channelCount, Memory<int> workspace,
        Memory<OctreeChunk?> chunks, int branchCapacity, bool initiallyEmpty = false)
    {
        ValidateLevels(levels, chunkLevels);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        var required = GetRequiredWorkspaceLength(branchCapacity, chunks.Length);
        if (workspace.Length < required) throw new ArgumentException("Workspace is too short.", nameof(workspace));
        Levels = levels; ChunkLevels = chunkLevels; ChannelCount = channelCount;
        _nodes = workspace[..(branchCapacity * 8)];
        _slotNext = workspace.Slice(branchCapacity * 8, chunks.Length); _chunks = chunks;
        _nodes.Span.Clear(); _chunks.Span.Clear();
        for (var i = 0; i < chunks.Length; i++) _slotNext.Span[i] = i + 1;
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
    /// <summary>Calculates caller workspace capacity without allocating.</summary>
    public static int GetRequiredWorkspaceLength(int branchCapacity, int chunkCapacity)
    {
        if (branchCapacity < 0 || chunkCapacity < 0) throw new ArgumentOutOfRangeException(nameof(branchCapacity));
        return checked(branchCapacity * 8 + chunkCapacity);
    }
    /// <summary>The outer world depth.</summary>
    public int Levels { get; }
    /// <summary>The world side length in voxels.</summary>
    public int SideLength => 1 << Levels;
    /// <summary>The total logical voxel count, independent of residency.</summary>
    public long VoxelCount => 1L << (3 * Levels);
    /// <summary>The depth shared by resident chunks.</summary>
    public int ChunkLevels { get; }
    /// <summary>The side length of each chunk.</summary>
    public int ChunkSideLength => 1 << ChunkLevels;
    /// <summary>The number of independent channels.</summary>
    public int ChannelCount { get; }
    /// <summary>Maximum resident nonempty chunks.</summary>
    public int ChunkCapacity => _chunks.Length;
    /// <summary>Maximum eight-child branches.</summary>
    public int BranchCapacity => _nodes.Length / 8;
    /// <summary>Number of retained nonempty chunk payloads.</summary>
    public int ResidentChunkCount { get; private set; }
    /// <summary>Number of live branches; collapsed/released branches are reusable.</summary>
    public int BranchCount { get; private set; }
    /// <summary>Changes after successful mutations. Useful for invalidating application-level caches.</summary>
    public ulong Revision { get; private set; }
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
        var mask = (1 << levels) - 1;
        if (((x | y | z) & mask) != 0) throw new ArgumentException("Region origins must be aligned to their side length.");
    }
    private void ChunkOrigin(int cx, int cy, int cz, out int x, out int y, out int z)
    {
        var count = 1 << (Levels - ChunkLevels);
        if ((uint)cx >= (uint)count || (uint)cy >= (uint)count || (uint)cz >= (uint)count)
            throw new ArgumentOutOfRangeException(nameof(cx), "Chunk coordinates must be inside the world.");
        x = cx << ChunkLevels; y = cy << ChunkLevels; z = cz << ChunkLevels;
    }
    private static int Child(int x, int y, int z, int bit) =>
        (((x >> bit) & 1) << 2) | (((y >> bit) & 1) << 1) | ((z >> bit) & 1);
    private int Find(int x, int y, int z, out int levels)
    {
        var link = _root; levels = Levels;
        while (link > 0) { levels--; link = _nodes.Span[link - 1 + Child(x, y, z, levels)]; }
        return link;
    }
    /// <summary>Gets the stored region containing a voxel, including its unloaded/empty state.</summary>
    public OctreeWorldRegion GetRegion(int x, int y, int z)
    {
        ValidatePoint(x, y, z); var link = Find(x, y, z, out var levels);
        return Region(link, (x >> levels) << levels, (y >> levels) << levels, (z >> levels) << levels, levels);
    }
    private OctreeWorldRegion Region(int link, int x, int y, int z, int levels) =>
        new(x, y, z, levels, link == 0 ? OctreeRegionKind.Unloaded : link == -1 ? OctreeRegionKind.Empty : OctreeRegionKind.Loaded,
            link < -1 ? _chunks.Span[-link - 2] : null);
    /// <summary>Gets a resident payload by chunk coordinates; false also covers known-empty chunks.</summary>
    public bool TryGetChunk(int chunkX, int chunkY, int chunkZ, out OctreeChunk? chunk)
    {
        ChunkOrigin(chunkX, chunkY, chunkZ, out var x, out var y, out var z);
        var link = Find(x, y, z, out _);
        chunk = link < -1 ? _chunks.Span[-link - 2] : null;
        return chunk is not null;
    }
    /// <summary>Loads or replaces one chunk without allocating. False means insufficient fixed capacity; no state is changed.</summary>
    public bool TryLoadChunk(int chunkX, int chunkY, int chunkZ, OctreeChunk chunk)
    {
        if (chunk is null) throw new ArgumentNullException(nameof(chunk));
        if (chunk.Levels != ChunkLevels || chunk.ChannelCount != ChannelCount) throw new ArgumentException("Chunk schema does not match the world.", nameof(chunk));
        ChunkOrigin(chunkX, chunkY, chunkZ, out var x, out var y, out var z);
        if (chunk.IsEmpty) return TrySetRegion(x, y, z, ChunkLevels, -1);
        var old = Find(x, y, z, out _);
        if (old < -1)
        {
            if (!ReferenceEquals(_chunks.Span[-old - 2], chunk)) { _chunks.Span[-old - 2] = chunk; Revision++; }
            return true;
        }
        if (_freeSlot == _chunks.Length || !CanSet(x, y, z, ChunkLevels, -2)) return false;
        var slot = _freeSlot; _freeSlot = _slotNext.Span[slot];
        _chunks.Span[slot] = chunk; ResidentChunkCount++;
        _root = Replace(_root, Levels, x, y, z, ChunkLevels, -slot - 2); Revision++;
        return true;
    }
    /// <summary>Loads a chunk, throwing if fixed capacity is exhausted.</summary>
    public void LoadChunk(int chunkX, int chunkY, int chunkZ, OctreeChunk chunk)
    {
        if (!TryLoadChunk(chunkX, chunkY, chunkZ, chunk)) throw new InvalidOperationException("World capacity is exhausted.");
    }
    /// <summary>Imports a stored region, optionally translated, into this larger world without copying its payload.</summary>
    /// <remarks>Loaded regions must match the destination chunk schema. Empty/unloaded outer regions preserve their depth. Each call is atomic; a sequence of imports is not.</remarks>
    public bool TryLoadRegion(OctreeWorldRegion region, int offsetX = 0, int offsetY = 0, int offsetZ = 0)
    {
        var x = checked(region.X + offsetX); var y = checked(region.Y + offsetY); var z = checked(region.Z + offsetZ);
        ValidateRegion(x, y, z, region.Levels);
        if (region.Kind == OctreeRegionKind.Empty) return TrySetEmptyRegion(x, y, z, region.Levels);
        if (region.Kind == OctreeRegionKind.Unloaded) return TryUnloadRegion(x, y, z, region.Levels);
        if (region.Levels != ChunkLevels || region.Chunk is null) throw new ArgumentException("Invalid loaded region.", nameof(region));
        return TryLoadChunk(x >> ChunkLevels, y >> ChunkLevels, z >> ChunkLevels, region.Chunk);
    }
    /// <summary>Marks an aligned region as known zero in every channel, releasing its retained payloads.</summary>
    public bool TrySetEmptyRegion(int x, int y, int z, int levels) => TrySetRegion(x, y, z, levels, -1);
    /// <summary>Marks an aligned region as unknown, releasing resident payloads. Save them first if needed.</summary>
    public bool TryUnloadRegion(int x, int y, int z, int levels) => TrySetRegion(x, y, z, levels, 0);
    private bool TrySetRegion(int x, int y, int z, int levels, int value)
    {
        ValidateRegion(x, y, z, levels);
        if (!CanSet(x, y, z, levels, value)) return false;
        var old = Find(x, y, z, out var oldLevels);
        if (oldLevels >= levels && old == value) return true;
        _root = Replace(_root, Levels, x, y, z, levels, value); Revision++;
        return true;
    }
    private bool CanSet(int x, int y, int z, int target, int value)
    {
        var link = _root; var level = Levels;
        while (level > target && link > 0) { level--; link = _nodes.Span[link - 1 + Child(x, y, z, level)]; }
        return level == target || link == value || AvailableBranches >= level - target;
    }
    private int AllocateBranch(int fill)
    {
        int link;
        if (_freeBranch != 0) { link = _freeBranch; _freeBranch = _nodes.Span[link - 1]; _freeBranchCount--; }
        else { link = _nextNode + 1; _nextNode += 8; }
        _nodes.Span.Slice(link - 1, 8).Fill(fill); BranchCount++; return link;
    }
    private void ReleaseBranch(int link)
    {
        _nodes.Span.Slice(link - 1, 8).Clear(); _nodes.Span[link - 1] = _freeBranch;
        _freeBranch = link; _freeBranchCount++; BranchCount--;
    }
    private void Release(int link)
    {
        if (link > 0)
        {
            for (var i = 0; i < 8; i++) Release(_nodes.Span[link - 1 + i]);
            ReleaseBranch(link);
        }
        else if (link < -1)
        {
            var slot = -link - 2; _chunks.Span[slot] = null;
            _slotNext.Span[slot] = _freeSlot; _freeSlot = slot; ResidentChunkCount--;
        }
    }
    private int Replace(int link, int level, int x, int y, int z, int target, int value)
    {
        if (link == value) return link;
        if (level == target) { Release(link); return value; }
        if (link <= 0) link = AllocateBranch(link);
        var childIndex = link - 1 + Child(x, y, z, level - 1);
        _nodes.Span[childIndex] = Replace(_nodes.Span[childIndex], level - 1, x, y, z, target, value);
        var first = _nodes.Span[link - 1];
        if (first != 0 && first != -1) return link;
        for (var i = 1; i < 8; i++) if (_nodes.Span[link - 1 + i] != first) return link;
        ReleaseBranch(link); return first;
    }
}
