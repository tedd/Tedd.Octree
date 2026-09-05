using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Tedd.Voxtree;

public sealed partial class Octree
{
    /// <summary>Builds one dense channel in the specified order, publishing one final encoded array.</summary>
    public void Build(ReadOnlySpan<uint> values, DenseVoxelLayout layout) =>
        Volatile.Write(ref _data, OctreeCodec.BuildOwned(values, _levels, layout));
    /// <summary>Measures encoded capacity for a linear or Morton-ordered source.</summary>
    public static int GetRequiredSize(ReadOnlySpan<uint> source, int levels, DenseVoxelLayout layout) =>
        OctreeCodec.GetRequiredSize(source, levels, layout);
    /// <summary>Builds a linear or Morton-ordered source into caller storage without allocations.</summary>
    public static int Build(ReadOnlySpan<uint> source, int levels, Span<byte> destination, DenseVoxelLayout layout)
    {
        if (TryBuild(source, levels, destination, out var written, layout)) return written;
        throw new ArgumentException("Encoded destination is too short.", nameof(destination));
    }
    /// <summary>Attempts a linear or Morton-ordered caller-buffer build. Capacity failure may modify destination.</summary>
    public static bool TryBuild(ReadOnlySpan<uint> source, int levels, Span<byte> destination, out int written, DenseVoxelLayout layout) =>
        OctreeCodec.TryBuild(source, levels, destination, out written, layout);
    /// <summary>Extracts a cubic block, in either dense order, without decoding the rest of the tree.</summary>
    public void CopyBlockTo(int x, int y, int z, int levels, Span<uint> destination, DenseVoxelLayout layout = DenseVoxelLayout.Linear) =>
        AsSpan().CopyBlockTo(x, y, z, levels, destination, layout);
}

public readonly ref partial struct OctreeSpan
{
    /// <summary>Extracts a cubic block relative to its own origin. The origin need not be aligned.</summary>
    /// <remarks>Bounds must fit inside this tree; levels is in 0..Octree.MaxLevels. Untrusted encodings need prior validation.</remarks>
    public void CopyBlockTo(int x, int y, int z, int levels, Span<uint> destination, DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        OctreeCodec.ValidateLevels(levels); DenseVoxel.Validate(layout);
        var side = 1 << levels;
        var box = new VoxelBox(x, y, z, checked(x + side), checked(y + side), checked(z + side));
        OctreeQueries.Validate(this, box);
        var count = OctreeCodec.GetVoxelCount(levels);
        if (destination.Length < count) throw new ArgumentException("Destination is too short.", nameof(destination));
        destination = destination[..count];
        if (MemoryMarshal.AsBytes(destination).Overlaps(_data)) throw new ArgumentException("Destination overlaps the encoding.");
        var state = new OctreeQueries.State(OctreeQueries.QueryKind.Copy, box, default)
        { Values = destination, OutputLayout = layout };
        OctreeQueries.Run(this, ref state);
    }
}
