using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Tedd.Voxtree;

/// <summary>An immutable multi-channel chunk of generic unmanaged voxel values.</summary>
public sealed class OctreeChunk<T> where T : unmanaged
{
    private const int HeaderSize = 12;
    private readonly ReadOnlyMemory<byte>[] _channels;

    /// <summary>Validates and retains channel encodings, copying only their memory descriptors.</summary>
    public OctreeChunk(int levels, ReadOnlySpan<ReadOnlyMemory<byte>> channels)
        : this(levels, ValidateAndCopy(levels, channels)) { }

    private OctreeChunk(int levels, ReadOnlyMemory<byte>[] channels)
    {
        VoxelType<T>.Validate();
        Levels = levels;
        _channels = channels;
        IsEmpty = true;
        var side = 1 << levels;
        var box = new VoxelBox(0, 0, 0, side, side, side);
        foreach (var channel in channels)
        {
            if (!new OctreeSpan<T>(channel.Span).Any(box, VoxelFilter<T>.NonZero)) continue;
            IsEmpty = false;
            break;
        }
    }

    private static ReadOnlyMemory<byte>[] ValidateAndCopy(int levels,
        ReadOnlySpan<ReadOnlyMemory<byte>> channels)
    {
        VoxelType<T>.Validate();
        OctreeCodec.ValidateLevels(levels);
        if (channels.IsEmpty) throw new ArgumentException("At least one channel is required.", nameof(channels));
        foreach (var channel in channels)
            if (!OctreeSpan<T>.TryCreate(channel.Span, out var view) || view.Levels != levels || !view.IsWellFormed())
                throw new FormatException("Every channel must be a well-formed encoding with the specified value width and depth.");
        return channels.ToArray();
    }

    /// <summary>The chunk depth.</summary>
    public int Levels { get; }
    /// <summary>The chunk side length.</summary>
    public int SideLength => 1 << Levels;
    /// <summary>The number of independent channels.</summary>
    public int ChannelCount => _channels.Length;
    /// <summary>Whether every channel value has an all-zero bit representation.</summary>
    public bool IsEmpty { get; }

    /// <summary>Creates an exclusively owned, mutable Morton-order copy for intensive editing.</summary>
    public HotOctreeChunk<T> MarkHot() => HotOctreeChunk<T>.FromChunk(this);

    /// <summary>Creates an owned snapshot from channel-major dense data.</summary>
    public static OctreeChunk<T> FromDense(int levels, int channelCount, ReadOnlySpan<T> values,
        DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        VoxelType<T>.Validate();
        OctreeCodec.ValidateLevels(levels);
        DenseVoxel.Validate(layout);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        var count = OctreeCodec.GetVoxelCount(levels);
        if (values.Length != checked(count * channelCount))
            throw new ArgumentException("Incorrect dense channel count.", nameof(values));
        var channels = new ReadOnlyMemory<byte>[channelCount];
        for (var index = 0; index < channelCount; index++)
            channels[index] = VoxelCodec<T>.BuildOwned(values.Slice(index * count, count), levels, layout);
        return new OctreeChunk<T>(levels, channels);
    }

    /// <summary>Creates an all-zero chunk without traversing a dense volume.</summary>
    public static OctreeChunk<T> Empty(int levels, int channelCount)
    {
        VoxelType<T>.Validate();
        OctreeCodec.ValidateLevels(levels);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        // Construct the canonical version-2 uniform-zero encoding directly.
        // A level-zero byte channel may otherwise select dense storage because
        // its dense and uniform encodings have equal length at that level.
        var bytes = new byte[GenericOctreeCodec<byte>.HeaderSize + 1];
        bytes[0] = OctreeCodec.Magic;
        bytes[1] = (byte)((Octree<T>.FormatVersion << 6)
            | (levels << 2) | (byte)StorageKind.Uniform);
        bytes[2] = VoxelType<T>.SizeCode;
        ReadOnlyMemory<byte> zero = bytes;
        var channels = new ReadOnlyMemory<byte>[channelCount];
        channels.AsSpan().Fill(zero);
        return new OctreeChunk<T>(levels, channels);
    }

    /// <summary>Gets a zero-copy view of a channel.</summary>
    public OctreeSpan<T> GetChannel(int channel)
    {
        var data = GetChannelData(channel).Span;
        return OctreeSpan<T>.CreateTrusted(data, Levels, VoxelCodec<T>.GetStorageKindUnchecked(data));
    }

    /// <summary>Gets one channel's encoded bytes.</summary>
    public ReadOnlyMemory<byte> GetChannelData(int channel)
    {
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
        return _channels[channel];
    }

    /// <summary>Rebuilds one dense channel and returns a snapshot sharing the other encodings.</summary>
    public OctreeChunk<T> WithDenseChannel(int channel, ReadOnlySpan<T> values,
        DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        _ = GetChannelData(channel);
        var encoded = VoxelCodec<T>.BuildOwned(values, Levels, layout);
        var channels = (ReadOnlyMemory<byte>[])_channels.Clone();
        channels[channel] = encoded;
        return new OctreeChunk<T>(Levels, channels);
    }

    /// <summary>Extracts every channel into consecutive dense block slices.</summary>
    public void CopyBlockTo(int x, int y, int z, int levels, Span<T> destination,
        DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        OctreeCodec.ValidateLevels(levels);
        DenseVoxel.Validate(layout);
        var side = 1 << levels;
        GenericOctreeQueries<T>.Validate(GetChannel(0),
            new VoxelBox(x, y, z, checked(x + side), checked(y + side), checked(z + side)));
        var count = OctreeCodec.GetVoxelCount(levels);
        var total = checked(count * ChannelCount);
        if (destination.Length < total) throw new ArgumentException("Destination is too short.", nameof(destination));
        destination = destination[..total];
        foreach (var channel in _channels)
            if (MemoryMarshal.AsBytes(destination).Overlaps(channel.Span))
                throw new ArgumentException("Destination overlaps a channel encoding.");
        for (var index = 0; index < ChannelCount; index++)
            GetChannel(index).CopyBlockTo(x, y, z, levels, destination.Slice(index * count, count), layout);
    }

    /// <summary>Gets the byte count for the versioned multi-channel packet.</summary>
    public int SerializedLength
    {
        get
        {
            var length = HeaderSize;
            foreach (var channel in _channels) length = checked(length + sizeof(int) + channel.Length);
            return length;
        }
    }

    /// <summary>Saves every channel and its shared depth into caller memory.</summary>
    public int CopyEncodedTo(Span<byte> destination)
    {
        var length = SerializedLength;
        if (destination.Length < length) throw new ArgumentException("Destination is too short.", nameof(destination));
        destination = destination[..length];
        foreach (var channel in _channels)
            if (destination.Overlaps(channel.Span)) throw new ArgumentException("Destination overlaps a channel encoding.");
        destination[0] = 0x4f;
        destination[1] = 0x43;
        destination[2] = 2;
        destination[3] = (byte)Levels;
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], ChannelCount);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], length);
        var offset = HeaderSize;
        foreach (var channel in _channels)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], channel.Length);
            offset += sizeof(int);
            channel.Span.CopyTo(destination[offset..]);
            offset += channel.Length;
        }
        return length;
    }

    /// <summary>Validates and loads a generic chunk packet without copying its channel payloads.</summary>
    public static OctreeChunk<T> FromEncoded(ReadOnlyMemory<byte> encoded)
    {
        VoxelType<T>.Validate();
        var data = encoded.Span;
        if (data.Length < HeaderSize || data[0] != 0x4f || data[1] != 0x43 || data[2] != 2 ||
            data[3] > Octree.MaxLevels || BinaryPrimitives.ReadInt32LittleEndian(data[8..]) != data.Length)
            throw new FormatException("Invalid generic chunk packet header.");
        var count = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        if (count <= 0 || count > (data.Length - HeaderSize) / 8)
            throw new FormatException("Invalid channel count.");
        var offset = HeaderSize;
        for (var index = 0; index < count; index++)
        {
            if (data.Length - offset < sizeof(int)) throw new FormatException("Truncated channel header.");
            var channelLength = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
            offset += sizeof(int);
            if (channelLength < 4 || channelLength > data.Length - offset ||
                !OctreeSpan<T>.TryCreate(data.Slice(offset, channelLength), out var channel) ||
                channel.Levels != data[3] || !channel.IsWellFormed())
                throw new FormatException("Invalid generic channel payload.");
            offset += channelLength;
        }
        if (offset != data.Length) throw new FormatException("Trailing chunk packet data.");
        var channels = new ReadOnlyMemory<byte>[count];
        offset = HeaderSize;
        for (var index = 0; index < count; index++)
        {
            var channelLength = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
            offset += sizeof(int);
            channels[index] = encoded.Slice(offset, channelLength);
            offset += channelLength;
        }
        return new OctreeChunk<T>(data[3], channels);
    }
}
