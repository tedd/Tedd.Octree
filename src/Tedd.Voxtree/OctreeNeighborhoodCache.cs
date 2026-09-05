using System;
using System.Runtime.CompilerServices;

namespace Tedd.Voxtree;

/// <summary>A persistent sliding neighborhood cache for entities or simulation workers.</summary>
/// <remarks>
/// Allocate once and reuse. Update detects owned-tree rebuilds; call it before each query batch.
/// Not thread-safe. Use OctreeNeighborhoodSpan for stack or unmanaged storage.
/// </remarks>
public sealed class OctreeNeighborhoodCache
{
    private readonly Memory<uint> _storage;
    private ReadOnlyMemory<byte> _snapshot;
    private NeighborhoodState _state;

    /// <summary>Allocates one reusable uint buffer and a cache wrapper.</summary>
    public OctreeNeighborhoodCache(int side)
        : this(new uint[OctreeNeighborhoodSpan.GetRequiredCount(side)], side) { }

    /// <summary>Uses caller-owned memory, which must remain alive and exclusive to the cache.</summary>
    public OctreeNeighborhoodCache(Memory<uint> storage, int side)
    {
        var count = OctreeNeighborhoodSpan.GetRequiredCount(side);
        if (storage.Length < count) throw new ArgumentException("Cache storage is too short.", nameof(storage));
        _storage = storage[..count];
        _state = new NeighborhoodState { Side = side };
    }

    /// <summary>Whether the cache contains a completed update.</summary>
    public bool IsValid => _state.Valid;
    /// <summary>The cached half-open voxel bounds.</summary>
    public VoxelBox Bounds => _state.Bounds;
    /// <summary>The fixed side length.</summary>
    public int SideLength => _state.Side;

    /// <summary>Updates around the center and returns the number of refreshed voxels. No steady-state allocation.</summary>
    public int Update(Octree source, int x, int y, int z)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var snapshot = source.Data;
        if (snapshot.IsEmpty) throw new InvalidOperationException("The source has not been built.");
        return Update(snapshot, 0, x, y, z);
    }

    /// <summary>Updates from a borrowed channel encoding; change revision after every in-place rebuild.</summary>
    /// <remarks>The encoded memory must remain alive and immutable while used by the cache.</remarks>
    public int Update(ReadOnlyMemory<byte> snapshot, ulong revision, int x, int y, int z)
    {
        var cache = new OctreeNeighborhoodSpan(_storage.Span, _snapshot.Span, _state);
        try
        {
            var copied = cache.Update(new OctreeSpan(snapshot.Span), revision, x, y, z);
            _snapshot = snapshot;
            return copied;
        }
        finally { _state = cache.State; }
    }

    /// <summary>Invalidates all cached contents until the next Update.</summary>
    public void Invalidate() => _state.Valid = false;

    /// <summary>Invalidates the cache if a changed region intersects it.</summary>
    public void Invalidate(VoxelBox changedRegion)
    {
        if (_state.Bounds.Intersects(changedRegion)) Invalidate();
    }

    /// <summary>Reads a cached voxel without a tree traversal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Get(int x, int y, int z)
    {
        if (!_state.Valid) return OctreeThrowHelper.ThrowInvalidView();
        if (!_state.Bounds.Contains(x, y, z))
            return OctreeThrowHelper.ThrowCoordinate(nameof(x), x);
        var side = _state.Side;
        var mask = side - 1;
        return _storage.Span[(((x & mask) * side + (y & mask)) * side) + (z & mask)];
    }

    /// <summary>Attempts to read a cached voxel without consulting the tree.</summary>
    public bool TryGet(int x, int y, int z, out uint value) => View().TryGet(x, y, z, out value);

    /// <summary>Tests a box entirely within cached bounds for any matching voxel.</summary>
    public bool Any(VoxelBox box, VoxelFilter filter) => View().Any(box, filter);

    private OctreeNeighborhoodSpan View() => new(_storage.Span, _snapshot.Span, _state);
}
