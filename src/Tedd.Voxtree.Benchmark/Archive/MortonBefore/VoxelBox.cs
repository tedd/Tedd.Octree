using System;

namespace Tedd.Voxtree.Benchmark.Archive.MortonBefore;

/// <summary>An integer voxel box with inclusive minima and exclusive maxima.</summary>
public readonly struct VoxelBox
{
    /// <summary>Creates a box. Empty boxes are permitted.</summary>
    public VoxelBox(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        if (maxX < minX || maxY < minY || maxZ < minZ)
            throw new ArgumentException("Each maximum must be greater than or equal to its minimum.");
        MinX = minX; MinY = minY; MinZ = minZ;
        MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
    }

    /// <summary>Inclusive X minimum.</summary>
    public int MinX { get; }
    /// <summary>Inclusive Y minimum.</summary>
    public int MinY { get; }
    /// <summary>Inclusive Z minimum.</summary>
    public int MinZ { get; }
    /// <summary>Exclusive X maximum.</summary>
    public int MaxX { get; }
    /// <summary>Exclusive Y maximum.</summary>
    public int MaxY { get; }
    /// <summary>Exclusive Z maximum.</summary>
    public int MaxZ { get; }
    /// <summary>Whether this box contains no voxels.</summary>
    public bool IsEmpty => MinX == MaxX || MinY == MaxY || MinZ == MaxZ;
    /// <summary>The voxel count. Throws if it exceeds Int32 capacity.</summary>
    public int Count => IsEmpty ? 0 : checked((MaxX - MinX) * (MaxY - MinY) * (MaxZ - MinZ));
    /// <summary>Tests a voxel coordinate against the half-open bounds.</summary>
    public bool Contains(int x, int y, int z) =>
        x >= MinX && x < MaxX && y >= MinY && y < MaxY && z >= MinZ && z < MaxZ;
    /// <summary>Tests whether two nonempty boxes intersect.</summary>
    public bool Intersects(VoxelBox other) => !IsEmpty && !other.IsEmpty &&
        MinX < other.MaxX && MaxX > other.MinX &&
        MinY < other.MaxY && MaxY > other.MinY &&
        MinZ < other.MaxZ && MaxZ > other.MinZ;

    internal bool Contains(VoxelBox other) =>
        other.MinX >= MinX && other.MaxX <= MaxX &&
        other.MinY >= MinY && other.MaxY <= MaxY &&
        other.MinZ >= MinZ && other.MaxZ <= MaxZ;

    internal VoxelBox Intersect(VoxelBox other)
    {
        var x = Math.Max(MinX, other.MinX);
        var y = Math.Max(MinY, other.MinY);
        var z = Math.Max(MinZ, other.MinZ);
        return new VoxelBox(x, y, z, Math.Max(x, Math.Min(MaxX, other.MaxX)),
            Math.Max(y, Math.Min(MaxY, other.MaxY)), Math.Max(z, Math.Min(MaxZ, other.MaxZ)));
    }

    internal long DistanceSquared(int x, int y, int z)
    {
        var dx = x - Clamp(x, MinX, MaxX - 1);
        var dy = y - Clamp(y, MinY, MaxY - 1);
        var dz = z - Clamp(z, MinZ, MaxZ - 1);
        return (long)dx * dx + (long)dy * dy + (long)dz * dz;
    }

    internal static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
}
