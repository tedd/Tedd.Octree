using System;

namespace Tedd.Octree;

public sealed partial class Octree
{
    /// <summary>Tests whether any voxel in a half-open box matches the filter. Stops at the first match.</summary>
    public bool Any(VoxelBox box, VoxelFilter filter) => AsSpan().Any(box, filter);

    /// <summary>Counts matching voxels, counting uniform regions without expanding them.</summary>
    public int CountMatches(VoxelBox box, VoxelFilter filter) => AsSpan().CountMatches(box, filter);

    /// <summary>Writes matching voxels into caller storage; returns false if additional matches exist.</summary>
    /// <remarks>Result order is deterministic for a given encoding but is not a sorting contract.</remarks>
    public bool Query(VoxelBox box, VoxelFilter filter, Span<VoxelHit> destination, out int written) =>
        AsSpan().Query(box, filter, destination, out written);

    /// <summary>Decodes a box into packed X/Y/Z-major caller storage.</summary>
    public void CopyRegionTo(VoxelBox box, Span<uint> destination) => AsSpan().CopyRegionTo(box, destination);

    /// <summary>Finds the nearest matching voxel center within an inclusive Euclidean radius.</summary>
    /// <remarks>The center must be inside the volume. Ties use ascending X, then Y, then Z.</remarks>
    public bool TryFindNearest(int x, int y, int z, int radius, VoxelFilter filter, out VoxelHit hit) =>
        AsSpan().TryFindNearest(x, y, z, radius, filter, out hit);
}
