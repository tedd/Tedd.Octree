using System;
using System.Runtime.InteropServices;

namespace Tedd.Octree;

public readonly ref partial struct OctreeSpan
{
    /// <summary>Tests whether any voxel in a half-open box matches the filter. Stops at the first match.</summary>
    /// <remarks>Bounds must lie inside the volume. Validate untrusted encodings with IsWellFormed first.</remarks>
    public bool Any(VoxelBox box, VoxelFilter filter)
    {
        OctreeQueries.Validate(this, box);
        // Tiny support footprints do not amortize the general visitor's octant setup.
        if (_storageKind == StorageKind.Tree && box.Count <= 4)
        {
            for (var x = box.MinX; x < box.MaxX; x++)
            for (var y = box.MinY; y < box.MaxY; y++)
            for (var z = box.MinZ; z < box.MaxZ; z++)
                if (filter.Matches(Get(x, y, z))) return true;
            return false;
        }
        var state = new OctreeQueries.State(OctreeQueries.QueryKind.Any, box, filter);
        OctreeQueries.Run(this, ref state);
        return state.Count != 0;
    }

    /// <summary>Counts matching voxels, counting uniform regions without expanding them.</summary>
    public int CountMatches(VoxelBox box, VoxelFilter filter)
    {
        OctreeQueries.Validate(this, box);
        var state = new OctreeQueries.State(OctreeQueries.QueryKind.Count, box, filter);
        OctreeQueries.Run(this, ref state);
        return state.Count;
    }

    /// <summary>Writes matching voxels into caller storage; returns false if additional matches exist.</summary>
    /// <remarks>
    /// Bounds must lie inside the volume. Results are valid even when the destination is too short;
    /// written is then its capacity. Order is deterministic for an encoding, not a sorting contract.
    /// Destination must not overlap encoded data. Malformed input may leave partial output.
    /// </remarks>
    public bool Query(VoxelBox box, VoxelFilter filter, Span<VoxelHit> destination, out int written)
    {
        OctreeQueries.Validate(this, box);
        if (MemoryMarshal.AsBytes(destination).Overlaps(_data))
            throw new ArgumentException("Destination must not overlap encoded data.", nameof(destination));
        var state = new OctreeQueries.State(OctreeQueries.QueryKind.Collect, box, filter) { Hits = destination };
        OctreeQueries.Run(this, ref state);
        written = state.Count;
        return !state.Truncated;
    }

    /// <summary>Decodes a box into packed X/Y/Z-major caller storage.</summary>
    /// <remarks>
    /// Only box.Count elements are written. Bounds must lie inside the volume and destination must
    /// not overlap encoded data. Validate untrusted input first; malformed input may leave partial output.
    /// </remarks>
    public void CopyRegionTo(VoxelBox box, Span<uint> destination)
    {
        OctreeQueries.Validate(this, box);
        if (destination.Length < box.Count)
            throw new ArgumentException("Destination is too short.", nameof(destination));
        destination = destination[..box.Count];
        if (MemoryMarshal.AsBytes(destination).Overlaps(_data))
            throw new ArgumentException("Destination must not overlap encoded data.", nameof(destination));
        var state = new OctreeQueries.State(OctreeQueries.QueryKind.Copy, box, default) { Values = destination };
        OctreeQueries.Run(this, ref state);
    }

    /// <summary>Finds the nearest matching voxel center within an inclusive Euclidean radius.</summary>
    /// <remarks>
    /// The center must be inside the volume; radius must be nonnegative. Ties use ascending X,
    /// then Y, then Z. This is a voxel-center search, not a continuous collision or swept-body test.
    /// </remarks>
    public bool TryFindNearest(int x, int y, int z, int radius, VoxelFilter filter, out VoxelHit hit)
    {
        var box = OctreeQueries.RadiusBox(this, x, y, z, radius);
        var centerValue = Get(x, y, z);
        if (filter.Matches(centerValue))
        {
            hit = new VoxelHit(x, y, z, centerValue);
            return true;
        }
        var state = new OctreeQueries.State(OctreeQueries.QueryKind.Nearest, box, filter)
        {
            X = x, Y = y, Z = z, BestDistance = (long)radius * radius
        };
        OctreeQueries.Run(this, ref state);
        hit = state.Nearest;
        return state.Found;
    }
}
