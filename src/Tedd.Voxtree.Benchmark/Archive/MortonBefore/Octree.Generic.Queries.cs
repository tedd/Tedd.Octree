using System;
using System.Runtime.InteropServices;

namespace Tedd.Voxtree.Benchmark.Archive.MortonBefore;

public sealed partial class Octree<T> where T : unmanaged
{
    /// <summary>Tests whether any voxel in a half-open box matches the filter.</summary>
    public bool Any(VoxelBox box, VoxelFilter<T> filter) => AsSpan().Any(box, filter);
    /// <summary>Counts matching voxels without expanding uniform regions.</summary>
    public int CountMatches(VoxelBox box, VoxelFilter<T> filter) => AsSpan().CountMatches(box, filter);
    /// <summary>Writes matching voxels into caller storage; false means additional matches exist.</summary>
    public bool Query(VoxelBox box, VoxelFilter<T> filter, Span<VoxelHit<T>> destination, out int written) =>
        AsSpan().Query(box, filter, destination, out written);
    /// <summary>Decodes a box into packed linear storage.</summary>
    public void CopyRegionTo(VoxelBox box, Span<T> destination) => AsSpan().CopyRegionTo(box, destination);
    /// <summary>Extracts a cubic block in linear or Morton order.</summary>
    public void CopyBlockTo(int x, int y, int z, int levels, Span<T> destination,
        DenseVoxelLayout layout = DenseVoxelLayout.Linear) =>
        AsSpan().CopyBlockTo(x, y, z, levels, destination, layout);
    /// <summary>Finds the nearest matching voxel center within an inclusive Euclidean radius.</summary>
    public bool TryFindNearest(int x, int y, int z, int radius, VoxelFilter<T> filter, out VoxelHit<T> hit) =>
        AsSpan().TryFindNearest(x, y, z, radius, filter, out hit);
}

public readonly ref partial struct OctreeSpan<T> where T : unmanaged
{
    /// <summary>Tests whether any voxel in a half-open box matches the filter.</summary>
    public bool Any(VoxelBox box, VoxelFilter<T> filter)
    {
        GenericOctreeQueries<T>.Validate(this, box);
        if (_storageKind == StorageKind.Tree && box.Count <= 4)
        {
            for (var x = box.MinX; x < box.MaxX; x++)
            for (var y = box.MinY; y < box.MaxY; y++)
            for (var z = box.MinZ; z < box.MaxZ; z++)
                if (filter.Matches(Get(x, y, z))) return true;
            return false;
        }
        var state = new GenericOctreeQueries<T>.State(GenericOctreeQueries<T>.QueryKind.Any, box, filter);
        GenericOctreeQueries<T>.Run(this, ref state);
        return state.Count != 0;
    }

    /// <summary>Counts matching voxels without expanding uniform regions.</summary>
    public int CountMatches(VoxelBox box, VoxelFilter<T> filter)
    {
        GenericOctreeQueries<T>.Validate(this, box);
        var state = new GenericOctreeQueries<T>.State(GenericOctreeQueries<T>.QueryKind.Count, box, filter);
        GenericOctreeQueries<T>.Run(this, ref state);
        return state.Count;
    }

    /// <summary>Writes matching voxels into caller storage; false means additional matches exist.</summary>
    public bool Query(VoxelBox box, VoxelFilter<T> filter, Span<VoxelHit<T>> destination, out int written)
    {
        GenericOctreeQueries<T>.Validate(this, box);
        if (MemoryMarshal.AsBytes(destination).Overlaps(_data))
            throw new ArgumentException("Destination must not overlap encoded data.", nameof(destination));
        var state = new GenericOctreeQueries<T>.State(GenericOctreeQueries<T>.QueryKind.Collect, box, filter)
        {
            Hits = destination
        };
        GenericOctreeQueries<T>.Run(this, ref state);
        written = state.Count;
        return !state.Truncated;
    }

    /// <summary>Decodes a box into packed X/Y/Z-major caller storage.</summary>
    public void CopyRegionTo(VoxelBox box, Span<T> destination)
    {
        GenericOctreeQueries<T>.Validate(this, box);
        if (destination.Length < box.Count)
            throw new ArgumentException("Destination is too short.", nameof(destination));
        destination = destination[..box.Count];
        if (MemoryMarshal.AsBytes(destination).Overlaps(_data))
            throw new ArgumentException("Destination must not overlap encoded data.", nameof(destination));
        var state = new GenericOctreeQueries<T>.State(GenericOctreeQueries<T>.QueryKind.Copy, box, default)
        {
            Values = destination
        };
        GenericOctreeQueries<T>.Run(this, ref state);
    }

    /// <summary>Extracts a cubic block relative to this tree's origin in linear or Morton order.</summary>
    public void CopyBlockTo(int x, int y, int z, int levels, Span<T> destination,
        DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        OctreeCodec.ValidateLevels(levels);
        DenseVoxel.Validate(layout);
        var side = 1 << levels;
        var box = new VoxelBox(x, y, z, checked(x + side), checked(y + side), checked(z + side));
        GenericOctreeQueries<T>.Validate(this, box);
        var count = OctreeCodec.GetVoxelCount(levels);
        if (destination.Length < count)
            throw new ArgumentException("Destination is too short.", nameof(destination));
        destination = destination[..count];
        if (MemoryMarshal.AsBytes(destination).Overlaps(_data))
            throw new ArgumentException("Destination overlaps the encoding.", nameof(destination));
        var state = new GenericOctreeQueries<T>.State(GenericOctreeQueries<T>.QueryKind.Copy, box, default)
        {
            Values = destination,
            OutputLayout = layout
        };
        GenericOctreeQueries<T>.Run(this, ref state);
    }

    /// <summary>Finds the nearest matching voxel center within an inclusive Euclidean radius.</summary>
    public bool TryFindNearest(int x, int y, int z, int radius, VoxelFilter<T> filter, out VoxelHit<T> hit)
    {
        var box = GenericOctreeQueries<T>.RadiusBox(this, x, y, z, radius);
        var centerValue = Get(x, y, z);
        if (filter.Matches(centerValue))
        {
            hit = new VoxelHit<T>(x, y, z, centerValue);
            return true;
        }
        var state = new GenericOctreeQueries<T>.State(GenericOctreeQueries<T>.QueryKind.Nearest, box, filter)
        {
            X = x,
            Y = y,
            Z = z,
            BestDistance = (long)radius * radius
        };
        GenericOctreeQueries<T>.Run(this, ref state);
        hit = state.Nearest;
        return state.Found;
    }
}
