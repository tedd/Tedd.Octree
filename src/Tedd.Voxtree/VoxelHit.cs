namespace Tedd.Octree;

/// <summary>A voxel coordinate and its channel value.</summary>
public readonly struct VoxelHit
{
    /// <summary>Creates a voxel result.</summary>
    public VoxelHit(int x, int y, int z, uint value)
    {
        X = x; Y = y; Z = z; Value = value;
    }
    /// <summary>X coordinate.</summary>
    public int X { get; }
    /// <summary>Y coordinate.</summary>
    public int Y { get; }
    /// <summary>Z coordinate.</summary>
    public int Z { get; }
    /// <summary>The stored channel value.</summary>
    public uint Value { get; }
}
