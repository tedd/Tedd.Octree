using System;
using System.Runtime.CompilerServices;

namespace Tedd.Voxtree;

/// <summary>The order of voxels within each dense channel.</summary>
public enum DenseVoxelLayout
{
    /// <summary>X/Y/Z-major: (x * side + y) * side + z.</summary>
    Linear,
    /// <summary>Tedd.MortonEncoding.Encode(x, y, z): X occupies the least significant interleaved bits.</summary>
    Morton
}

/// <summary>A mutable, allocation-free view over channel-major dense cubic data.</summary>
/// <remarks>All channels use the same layout. Caller storage remains exclusive while editing.</remarks>
public ref struct DenseVoxelBlockSpan
{
    private readonly Span<uint> _values;
    /// <summary>Creates a view over exactly channelCount * side cubed values.</summary>
    public DenseVoxelBlockSpan(Span<uint> values, int levels, int channelCount = 1, DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        OctreeCodec.ValidateLevels(levels);
        DenseVoxel.Validate(layout);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        if (values.Length != checked(OctreeCodec.GetVoxelCount(levels) * channelCount))
            throw new ArgumentException("Storage must exactly match the block and channel count.", nameof(values));
        _values = values; Levels = levels; ChannelCount = channelCount; Layout = layout;
    }
    /// <summary>The block depth.</summary>
    public readonly int Levels { get; }
    /// <summary>The block side length.</summary>
    public readonly int SideLength => 1 << Levels;
    /// <summary>The number of independent channels.</summary>
    public readonly int ChannelCount { get; }
    /// <summary>The dense order used by every channel.</summary>
    public readonly DenseVoxelLayout Layout { get; }
    /// <summary>Reads or writes one channel's voxel.</summary>
    public uint this[int channel, int x, int y, int z]
    {
        readonly get => GetChannelSpan(channel)[DenseVoxel.GetIndex(x, y, z, Levels, Layout)];
        set => GetChannelSpan(channel)[DenseVoxel.GetIndex(x, y, z, Levels, Layout)] = value;
    }
    /// <summary>Obtains one channel's contiguous dense slice for bulk editing.</summary>
    public readonly Span<uint> GetChannelSpan(int channel)
    {
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
        var count = OctreeCodec.GetVoxelCount(Levels);
        return _values.Slice(channel * count, count);
    }
}

/// <summary>Allocation-free indexing and conversion of dense cubic voxel buffers.</summary>
public static class DenseVoxel
{
    internal static void Validate(DenseVoxelLayout layout)
    {
        if (layout != DenseVoxelLayout.Linear && layout != DenseVoxelLayout.Morton)
            throw new ArgumentOutOfRangeException(nameof(layout));
    }
    /// <summary>Calculates a checked dense-block index, using the Morton package when requested.</summary>
    public static int GetIndex(int x, int y, int z, int levels, DenseVoxelLayout layout)
    {
        OctreeCodec.ValidateLevels(levels); Validate(layout);
        var side = 1 << levels;
        if ((uint)x >= (uint)side || (uint)y >= (uint)side || (uint)z >= (uint)side)
            throw new ArgumentOutOfRangeException(nameof(x));
        return Index(x, y, z, side, layout);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Index(int x, int y, int z, int side, DenseVoxelLayout layout) =>
        layout == DenseVoxelLayout.Linear ? (x * side + y) * side + z :
        (int)global::Tedd.MortonEncoding.Encode((uint)x, (uint)y, (uint)z);

    /// <summary>Converts one channel between linear and Morton order into caller storage.</summary>
    /// <remarks>Different layouts cannot overlap. Equal layouts permit overlap, like Span.CopyTo.</remarks>
    public static void Convert(ReadOnlySpan<uint> source, Span<uint> destination, int levels,
        DenseVoxelLayout sourceLayout, DenseVoxelLayout destinationLayout)
    {
        OctreeCodec.ValidateLevels(levels); Validate(sourceLayout); Validate(destinationLayout);
        var count = OctreeCodec.GetVoxelCount(levels);
        if (source.Length != count || destination.Length < count) throw new ArgumentException("Incorrect buffer capacity.");
        destination = destination[..count];
        if (sourceLayout == destinationLayout) { source.CopyTo(destination); return; }
        if (source.Overlaps(destination)) throw new ArgumentException("Layout conversion buffers must not overlap.");
        ConvertLayout(source, destination, levels, sourceLayout);
    }

    // Both spans are validated, disjoint, and have different layouts.
    internal static void ConvertLayout<T>(ReadOnlySpan<T> source, Span<T> destination, int levels,
        DenseVoxelLayout sourceLayout) where T : unmanaged
    {
        var side = 1 << levels;
        // Interleave each coordinate once, rather than encoding all three axes per voxel.
        // At MaxLevels this uses only 2 KiB of stack space.
        Span<int> axis = stackalloc int[side];
        for (var i = 0; i < side; i++)
            axis[i] = (int)global::Tedd.MortonEncoding.Encode((uint)i, 0u, 0u);
        var linear = 0;
        if (sourceLayout == DenseVoxelLayout.Linear)
        {
            for (var x = 0; x < side; x++)
            for (var y = 0; y < side; y++)
            {
                var xy = axis[x] | (axis[y] << 1);
                for (var z = 0; z < side; z++)
                    destination[xy | (axis[z] << 2)] = source[linear++];
            }
            return;
        }
        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        {
            var xy = axis[x] | (axis[y] << 1);
            for (var z = 0; z < side; z++)
                destination[linear++] = source[xy | (axis[z] << 2)];
        }
    }
}
