using System;

namespace Tedd.Voxtree.Benchmark.Archive.MortonBefore;

/// <summary>Allocation-free indexing and conversion of dense cubic <typeparamref name="T"/> buffers.</summary>
public static class DenseVoxel<T> where T : unmanaged
{
    /// <summary>Calculates a checked dense-block index.</summary>
    public static int GetIndex(int x, int y, int z, int levels, DenseVoxelLayout layout) =>
        DenseVoxel.GetIndex(x, y, z, levels, layout);

    /// <summary>Converts one channel between linear and Morton order into caller storage.</summary>
    public static void Convert(ReadOnlySpan<T> source, Span<T> destination, int levels,
        DenseVoxelLayout sourceLayout, DenseVoxelLayout destinationLayout) =>
        DenseVoxelGenericExtensions.Convert(source, destination, levels, sourceLayout, destinationLayout);
}
