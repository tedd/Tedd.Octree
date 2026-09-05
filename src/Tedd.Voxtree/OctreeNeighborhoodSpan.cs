using System;
using System.Runtime.InteropServices;

namespace Tedd.Voxtree;

/// <summary>A zero-allocation sliding voxel cache over caller-provided uint storage.</summary>
/// <remarks>
/// Update before using cached values. Owned trees invalidate automatically after rebuild; borrowed
/// encodings require a revision that changes whenever their bytes change. This mutable ref struct
/// is not thread-safe and must not be copied while in use. Storage must remain exclusive to it.
/// </remarks>
public ref struct OctreeNeighborhoodSpan
{
    private readonly Span<uint> _storage;
    private ReadOnlySpan<byte> _source;
    private NeighborhoodState _state;

    /// <summary>Creates a cache whose side is a power of two. Requires side cubed uint elements.</summary>
    public OctreeNeighborhoodSpan(Span<uint> storage, int side)
    {
        var count = GetRequiredCount(side);
        if (storage.Length < count)
            throw new ArgumentException("Cache storage is too short.", nameof(storage));
        _storage = storage[..count];
        _source = default;
        _state = new NeighborhoodState { Side = side };
    }

    internal OctreeNeighborhoodSpan(Span<uint> storage, ReadOnlySpan<byte> source, NeighborhoodState state)
    {
        _storage = storage; _source = source; _state = state;
    }

    internal readonly NeighborhoodState State => _state;

    /// <summary>Whether Update completed successfully since the last invalidation.</summary>
    public readonly bool IsValid => _state.Valid;
    /// <summary>The cached half-open bounds, or an empty box before the first update.</summary>
    public readonly VoxelBox Bounds => _state.Bounds;
    /// <summary>The fixed cache side length.</summary>
    public readonly int SideLength => _state.Side;

    /// <summary>Calculates storage capacity for a power-of-two side in 1..512.</summary>
    public static int GetRequiredCount(int side)
    {
        if (side <= 0 || side > (1 << Octree.MaxLevels) || (side & (side - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(side), "Cache side must be a power of two in 1..512.");
        return side * side * side;
    }

    /// <summary>Invalidates cached contents. The next Update refreshes the complete window.</summary>
    public void Invalidate() => _state.Valid = false;

    /// <summary>Invalidates only if the changed region intersects this cache.</summary>
    public void Invalidate(VoxelBox changedRegion)
    {
        if (_state.Bounds.Intersects(changedRegion))
            Invalidate();
    }

    /// <summary>Centers the window on a voxel, clamps it at chunk edges, and returns the number of refreshed voxels.</summary>
    /// <remarks>Reusing the same owned encoding performs no work; a successful rebuild invalidates it automatically.</remarks>
    public int Update(Octree source, int x, int y, int z)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Update(source.AsSpan(), 0, x, y, z);
    }

    /// <summary>Updates a borrowed encoding, using a caller-managed revision for in-place changes.</summary>
    /// <remarks>
    /// Increment revision for every change to the encoding, including rebuilding into the same buffer.
    /// The encoded input must remain immutable during this operation and must not overlap cache storage.
    /// </remarks>
    public int Update(OctreeSpan source, ulong revision, int x, int y, int z)
    {
        if (_state.Side == 0)
            throw new InvalidOperationException("The cache is not initialized.");
        if (!source.Contains(x, y, z))
            throw new ArgumentOutOfRangeException(nameof(x), "The cache center must lie inside a valid source.");
        if (_state.Side > source.SideLength)
            throw new ArgumentException("The cache must fit inside the source volume.", nameof(source));
        if (MemoryMarshal.AsBytes(_storage).Overlaps(source.Data))
            throw new ArgumentException("Cache storage must not overlap encoded data.", nameof(source));

        var side = _state.Side;
        var maxOrigin = source.SideLength - side;
        var minX = VoxelBox.Clamp(x - side / 2, 0, maxOrigin);
        var minY = VoxelBox.Clamp(y - side / 2, 0, maxOrigin);
        var minZ = VoxelBox.Clamp(z - side / 2, 0, maxOrigin);
        var next = new VoxelBox(minX, minY, minZ, minX + side, minY + side, minZ + side);
        var sameSource = _source.Length == source.Data.Length &&
            _source.Overlaps(source.Data, out var offset) && offset == 0 && revision == _state.Revision;
        var previous = _state.Bounds;
        if (_state.Valid && sameSource && previous.MinX == next.MinX &&
            previous.MinY == next.MinY && previous.MinZ == next.MinZ)
            return 0;
        var reuse = _state.Valid && sameSource && previous.Intersects(next);
        _state.Valid = false; // A malformed source must never leave a usable, partially refreshed cache.
        var copied = 0;
        if (!reuse)
            copied = Fill(source, next);
        else
        {
            var overlap = previous.Intersect(next);
            // Six disjoint slabs cover next minus overlap. Ring indexing preserves the overlap in place.
            copied += Fill(source, new VoxelBox(next.MinX, next.MinY, next.MinZ, overlap.MinX, next.MaxY, next.MaxZ));
            copied += Fill(source, new VoxelBox(overlap.MaxX, next.MinY, next.MinZ, next.MaxX, next.MaxY, next.MaxZ));
            copied += Fill(source, new VoxelBox(overlap.MinX, next.MinY, next.MinZ, overlap.MaxX, overlap.MinY, next.MaxZ));
            copied += Fill(source, new VoxelBox(overlap.MinX, overlap.MaxY, next.MinZ, overlap.MaxX, next.MaxY, next.MaxZ));
            copied += Fill(source, new VoxelBox(overlap.MinX, overlap.MinY, next.MinZ, overlap.MaxX, overlap.MaxY, overlap.MinZ));
            copied += Fill(source, new VoxelBox(overlap.MinX, overlap.MinY, overlap.MaxZ, overlap.MaxX, overlap.MaxY, next.MaxZ));
        }
        _source = source.Data;
        _state.Bounds = next;
        _state.Revision = revision;
        _state.Valid = true;
        return copied;
    }

    private int Fill(OctreeSpan source, VoxelBox box)
    {
        if (box.IsEmpty) return 0;
        var state = new OctreeQueries.State(OctreeQueries.QueryKind.Copy, box, default)
        {
            Values = _storage, RingSide = _state.Side
        };
        OctreeQueries.Run(source, ref state);
        return box.Count;
    }

    /// <summary>Reads a cached voxel. Update must have completed and the coordinate must lie in Bounds.</summary>
    public readonly uint Get(int x, int y, int z)
    {
        if (!IsValid) throw new InvalidOperationException("Update the cache before reading it.");
        if (!_state.Bounds.Contains(x, y, z))
            throw new ArgumentOutOfRangeException(nameof(x), "The coordinate is outside the cache.");
        return _storage[Index(x, y, z)];
    }

    /// <summary>Attempts to read a cached voxel without consulting the tree.</summary>
    public readonly bool TryGet(int x, int y, int z, out uint value)
    {
        value = default;
        if (!IsValid || !_state.Bounds.Contains(x, y, z)) return false;
        value = _storage[Index(x, y, z)];
        return true;
    }

    /// <summary>Tests occupancy within cached bounds, stopping at the first match.</summary>
    public readonly bool Any(VoxelBox box, VoxelFilter filter)
    {
        if (!IsValid) throw new InvalidOperationException("Update the cache before querying it.");
        if (!_state.Bounds.Contains(box))
            throw new ArgumentOutOfRangeException(nameof(box), "Query bounds must lie within the cache.");
        if (box.IsEmpty) return false;
        var side = _state.Side;
        for (var x = box.MinX; x < box.MaxX; x++)
        for (var y = box.MinY; y < box.MaxY; y++)
        {
            var length = box.MaxZ - box.MinZ;
            var first = Math.Min(length, side - (box.MinZ & (side - 1)));
            if (filter.AnyIn(_storage.Slice(Index(x, y, box.MinZ), first)) ||
                (first != length && filter.AnyIn(_storage.Slice(Index(x, y, box.MinZ + first), length - first))))
                return true;
        }
        return false;
    }

    private readonly int Index(int x, int y, int z)
    {
        var side = _state.Side;
        var mask = side - 1;
        return (((x & mask) * side + (y & mask)) * side) + (z & mask);
    }
}

internal struct NeighborhoodState
{
    internal int Side;
    internal VoxelBox Bounds;
    internal ulong Revision;
    internal bool Valid;
}
