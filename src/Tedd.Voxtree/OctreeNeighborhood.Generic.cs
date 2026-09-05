using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tedd.Voxtree;

/// <summary>A zero-allocation sliding cache over caller-provided generic voxel storage.</summary>
public ref struct OctreeNeighborhoodSpan<T> where T : unmanaged
{
    private readonly Span<T> _storage;
    private ReadOnlySpan<byte> _source;
    private NeighborhoodState _state;

    /// <summary>Creates a cache whose side is a power of two.</summary>
    public OctreeNeighborhoodSpan(Span<T> storage, int side)
    {
        VoxelType<T>.Validate();
        var count = GetRequiredCount(side);
        if (storage.Length < count) throw new ArgumentException("Cache storage is too short.", nameof(storage));
        _storage = storage[..count];
        _source = default;
        _state = new NeighborhoodState { Side = side };
    }

    internal OctreeNeighborhoodSpan(Span<T> storage, ReadOnlySpan<byte> source, NeighborhoodState state)
    {
        _storage = storage;
        _source = source;
        _state = state;
    }

    internal readonly NeighborhoodState State => _state;
    /// <summary>Whether Update completed successfully.</summary>
    public readonly bool IsValid => _state.Valid;
    /// <summary>The cached half-open bounds.</summary>
    public readonly VoxelBox Bounds => _state.Bounds;
    /// <summary>The fixed side length.</summary>
    public readonly int SideLength => _state.Side;

    /// <summary>Calculates storage capacity for a power-of-two side in 1..512.</summary>
    public static int GetRequiredCount(int side)
    {
        if (side <= 0 || side > (1 << Octree.MaxLevels) || (side & (side - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(side), "Cache side must be a power of two in 1..512.");
        return side * side * side;
    }

    /// <summary>Invalidates all cached content.</summary>
    public void Invalidate() => _state.Valid = false;
    /// <summary>Invalidates the cache if a changed region intersects it.</summary>
    public void Invalidate(VoxelBox changedRegion)
    {
        if (_state.Bounds.Intersects(changedRegion)) Invalidate();
    }

    /// <summary>Updates from an owned tree.</summary>
    public int Update(Octree<T> source, int x, int y, int z)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Update(source.AsSpan(), 0, x, y, z);
    }

    /// <summary>Updates from a borrowed encoding and caller-managed revision.</summary>
    public int Update(OctreeSpan<T> source, ulong revision, int x, int y, int z)
    {
        if (_state.Side == 0) throw new InvalidOperationException("The cache is not initialized.");
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
            previous.MinY == next.MinY && previous.MinZ == next.MinZ) return 0;
        var reuse = _state.Valid && sameSource && previous.Intersects(next);
        _state.Valid = false;
        var copied = 0;
        if (!reuse) copied = Fill(source, next);
        else
        {
            var overlap = previous.Intersect(next);
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

    private int Fill(OctreeSpan<T> source, VoxelBox box)
    {
        if (box.IsEmpty) return 0;
        var state = new GenericOctreeQueries<T>.State(GenericOctreeQueries<T>.QueryKind.Copy, box, default)
        {
            Values = _storage,
            RingSide = _state.Side
        };
        GenericOctreeQueries<T>.Run(source, ref state);
        return box.Count;
    }

    /// <summary>Reads one cached voxel.</summary>
    public readonly T Get(int x, int y, int z)
    {
        if (!IsValid) throw new InvalidOperationException("Update the cache before reading it.");
        if (!_state.Bounds.Contains(x, y, z))
            throw new ArgumentOutOfRangeException(nameof(x), "The coordinate is outside the cache.");
        return _storage[Index(x, y, z)];
    }

    /// <summary>Attempts to read one cached voxel.</summary>
    public readonly bool TryGet(int x, int y, int z, out T value)
    {
        value = default;
        if (!IsValid || !_state.Bounds.Contains(x, y, z)) return false;
        value = _storage[Index(x, y, z)];
        return true;
    }

    /// <summary>Tests cached bounds for any matching voxel.</summary>
    public readonly bool Any(VoxelBox box, VoxelFilter<T> filter)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly int Index(int x, int y, int z)
    {
        var side = _state.Side;
        var mask = side - 1;
        return (((x & mask) * side + (y & mask)) * side) + (z & mask);
    }
}

/// <summary>A persistent sliding generic neighborhood cache.</summary>
public sealed class OctreeNeighborhoodCache<T> where T : unmanaged
{
    private readonly Memory<T> _storage;
    private ReadOnlyMemory<byte> _snapshot;
    private NeighborhoodState _state;

    /// <summary>Allocates one reusable value buffer.</summary>
    public OctreeNeighborhoodCache(int side)
        : this(new T[OctreeNeighborhoodSpan<T>.GetRequiredCount(side)], side) { }

    /// <summary>Uses caller-owned memory that remains exclusive to this cache.</summary>
    public OctreeNeighborhoodCache(Memory<T> storage, int side)
    {
        VoxelType<T>.Validate();
        var count = OctreeNeighborhoodSpan<T>.GetRequiredCount(side);
        if (storage.Length < count) throw new ArgumentException("Cache storage is too short.", nameof(storage));
        _storage = storage[..count];
        _state = new NeighborhoodState { Side = side };
    }

    /// <summary>Whether the cache contains a completed update.</summary>
    public bool IsValid => _state.Valid;
    /// <summary>The cached half-open bounds.</summary>
    public VoxelBox Bounds => _state.Bounds;
    /// <summary>The fixed side length.</summary>
    public int SideLength => _state.Side;

    /// <summary>Updates from an owned tree.</summary>
    public int Update(Octree<T> source, int x, int y, int z)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var snapshot = source.Data;
        if (snapshot.IsEmpty) throw new InvalidOperationException("The source has not been built.");
        return Update(snapshot, 0, x, y, z);
    }

    /// <summary>Updates from a borrowed encoding and revision.</summary>
    public int Update(ReadOnlyMemory<byte> snapshot, ulong revision, int x, int y, int z)
    {
        var cache = new OctreeNeighborhoodSpan<T>(_storage.Span, _snapshot.Span, _state);
        try
        {
            var copied = cache.Update(new OctreeSpan<T>(snapshot.Span), revision, x, y, z);
            _snapshot = snapshot;
            return copied;
        }
        finally { _state = cache.State; }
    }

    /// <summary>Invalidates all cached content.</summary>
    public void Invalidate() => _state.Valid = false;
    /// <summary>Invalidates this cache if a changed region intersects it.</summary>
    public void Invalidate(VoxelBox changedRegion)
    {
        if (_state.Bounds.Intersects(changedRegion)) Invalidate();
    }
    /// <summary>Reads one cached voxel.</summary>
    public T Get(int x, int y, int z) => View().Get(x, y, z);
    /// <summary>Attempts to read one cached voxel.</summary>
    public bool TryGet(int x, int y, int z, out T value) => View().TryGet(x, y, z, out value);
    /// <summary>Tests cached bounds for any matching voxel.</summary>
    public bool Any(VoxelBox box, VoxelFilter<T> filter) => View().Any(box, filter);
    private OctreeNeighborhoodSpan<T> View() => new(_storage.Span, _snapshot.Span, _state);
}
