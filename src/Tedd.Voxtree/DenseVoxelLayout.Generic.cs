using System;

namespace Tedd.Voxtree;

/// <summary>A mutable, allocation-free view over channel-major dense cubic <typeparamref name="T"/> data.</summary>
public ref struct DenseVoxelBlockSpan<T> where T : unmanaged
{
    private readonly Span<T> _values;

    /// <summary>Creates a view over exactly channelCount times side-cubed values.</summary>
    public DenseVoxelBlockSpan(Span<T> values, int levels, int channelCount = 1,
        DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        VoxelType<T>.Validate();
        OctreeCodec.ValidateLevels(levels);
        DenseVoxel.Validate(layout);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        if (values.Length != checked(OctreeCodec.GetVoxelCount(levels) * channelCount))
            throw new ArgumentException("Storage must exactly match the block and channel count.", nameof(values));
        _values = values;
        Levels = levels;
        ChannelCount = channelCount;
        Layout = layout;
    }

    /// <summary>The block depth.</summary>
    public readonly int Levels { get; }
    /// <summary>The block side length.</summary>
    public readonly int SideLength => 1 << Levels;
    /// <summary>The number of independent channels.</summary>
    public readonly int ChannelCount { get; }
    /// <summary>The dense order used by every channel.</summary>
    public readonly DenseVoxelLayout Layout { get; }

    /// <summary>Reads or writes one channel voxel.</summary>
    public T this[int channel, int x, int y, int z]
    {
        readonly get => GetChannelSpan(channel)[DenseVoxel.GetIndex(x, y, z, Levels, Layout)];
        set => GetChannelSpan(channel)[DenseVoxel.GetIndex(x, y, z, Levels, Layout)] = value;
    }

    /// <summary>Obtains one channel's contiguous dense slice.</summary>
    public readonly Span<T> GetChannelSpan(int channel)
    {
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
        var count = OctreeCodec.GetVoxelCount(Levels);
        return _values.Slice(channel * count, count);
    }
}

internal static class DenseVoxelGenericExtensions
{
    /// <summary>Converts one generic channel between linear and Morton order into caller storage.</summary>
    public static void Convert<T>(ReadOnlySpan<T> source, Span<T> destination, int levels,
        DenseVoxelLayout sourceLayout, DenseVoxelLayout destinationLayout) where T : unmanaged
    {
        VoxelType<T>.Validate();
        OctreeCodec.ValidateLevels(levels);
        DenseVoxel.Validate(sourceLayout);
        DenseVoxel.Validate(destinationLayout);
        var count = OctreeCodec.GetVoxelCount(levels);
        if (source.Length != count || destination.Length < count)
            throw new ArgumentException("Incorrect buffer capacity.");
        destination = destination[..count];
        if (sourceLayout == destinationLayout)
        {
            source.CopyTo(destination);
            return;
        }
        if (source.Overlaps(destination))
            throw new ArgumentException("Layout conversion buffers must not overlap.");
        var side = 1 << levels;
        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
            destination[DenseVoxel.Index(x, y, z, side, destinationLayout)] =
                source[DenseVoxel.Index(x, y, z, side, sourceLayout)];
    }
}
