using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Tedd.Octree;

/// <summary>An immutable multi-channel chunk whose channels share one depth and coordinate system.</summary>
/// <remarks>Encoded channel memory is borrowed and must remain alive and immutable. Dense factories own their result arrays.</remarks>
public sealed class OctreeChunk
{
    private const int HeaderSize = 12;
    private readonly ReadOnlyMemory<byte>[] _channels;

    /// <summary>Validates and retains channel encodings, copying only the memory descriptors.</summary>
    public OctreeChunk(int levels, ReadOnlySpan<ReadOnlyMemory<byte>> channels)
        : this(levels, ValidateAndCopy(levels, channels)) { }

    private OctreeChunk(int levels, ReadOnlyMemory<byte>[] channels)
    {
        Levels = levels; _channels = channels;
        IsEmpty = true;
        var side = 1 << levels;
        var box = new VoxelBox(0, 0, 0, side, side, side);
        foreach (var channel in channels)
            if (new OctreeSpan(channel.Span).Any(box, VoxelFilter.NonZero)) { IsEmpty = false; break; }
    }

    private static ReadOnlyMemory<byte>[] ValidateAndCopy(int levels, ReadOnlySpan<ReadOnlyMemory<byte>> channels)
    {
        OctreeCodec.ValidateLevels(levels);
        if (channels.IsEmpty) throw new ArgumentException("At least one channel is required.", nameof(channels));
        foreach (var channel in channels)
            if (!OctreeSpan.TryCreate(channel.Span, out var view) || view.Levels != levels || !view.IsWellFormed())
                throw new FormatException("Every channel must be a well-formed encoding with the specified depth.");
        return channels.ToArray();
    }

    /// <summary>The chunk depth, in 0..Octree.MaxLevels.</summary>
    public int Levels { get; }
    /// <summary>The chunk side length.</summary>
    public int SideLength => 1 << Levels;
    /// <summary>The number of independent channels.</summary>
    public int ChannelCount => _channels.Length;
    /// <summary>Whether every voxel in every channel is zero.</summary>
    public bool IsEmpty { get; }
    /// <summary>Creates an owned snapshot from channel-major dense data in either order.</summary>
    public static OctreeChunk FromDense(int levels, int channelCount, ReadOnlySpan<uint> values, DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        OctreeCodec.ValidateLevels(levels); DenseVoxel.Validate(layout);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        var count = OctreeCodec.GetVoxelCount(levels);
        if (values.Length != checked(count * channelCount)) throw new ArgumentException("Incorrect dense channel count.", nameof(values));
        var channels = new ReadOnlyMemory<byte>[channelCount];
        for (var i = 0; i < channelCount; i++) channels[i] = OctreeCodec.BuildOwned(values.Slice(i * count, count), levels, layout);
        return new OctreeChunk(levels, channels);
    }
    /// <summary>Creates an all-zero chunk without allocating or traversing a dense volume.</summary>
    public static OctreeChunk Empty(int levels, int channelCount)
    {
        OctreeCodec.ValidateLevels(levels);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        ReadOnlyMemory<byte> zero = new byte[] { 0x4f, (byte)(0x41 | (levels << 2)), 0 };
        var channels = new ReadOnlyMemory<byte>[channelCount];
        channels.AsSpan().Fill(zero);
        return new OctreeChunk(levels, channels);
    }
    /// <summary>Gets a zero-copy view of a channel.</summary>
    public OctreeSpan GetChannel(int channel)
    {
        var data = GetChannelData(channel).Span;
        return OctreeSpan.CreateTrusted(data, Levels, OctreeCodec.GetStorageKindUnchecked(data));
    }
    /// <summary>Gets a channel's encoded bytes for persistence or caching.</summary>
    public ReadOnlyMemory<byte> GetChannelData(int channel)
    {
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
        return _channels[channel];
    }
    /// <summary>Rebuilds only one changed dense channel and returns a new snapshot sharing the other encodings.</summary>
    public OctreeChunk WithDenseChannel(int channel, ReadOnlySpan<uint> values, DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        _ = GetChannelData(channel);
        var encoded = OctreeCodec.BuildOwned(values, Levels, layout);
        var channels = (ReadOnlyMemory<byte>[])_channels.Clone();
        channels[channel] = encoded;
        return new OctreeChunk(Levels, channels);
    }
    /// <summary>Extracts all channels into consecutive dense block slices in the selected order.</summary>
    public void CopyBlockTo(int x, int y, int z, int levels, Span<uint> destination, DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        OctreeCodec.ValidateLevels(levels); DenseVoxel.Validate(layout);
        var side = 1 << levels;
        OctreeQueries.Validate(GetChannel(0), new VoxelBox(x, y, z, checked(x + side), checked(y + side), checked(z + side)));
        var count = OctreeCodec.GetVoxelCount(levels);
        var total = checked(count * ChannelCount);
        if (destination.Length < total) throw new ArgumentException("Destination is too short.", nameof(destination));
        destination = destination[..total];
        foreach (var channel in _channels)
            if (MemoryMarshal.AsBytes(destination).Overlaps(channel.Span)) throw new ArgumentException("Destination overlaps a channel encoding.");
        for (var i = 0; i < ChannelCount; i++) GetChannel(i).CopyBlockTo(x, y, z, levels, destination.Slice(i * count, count), layout);
    }
    /// <summary>Gets the byte count for the versioned multi-channel chunk packet.</summary>
    public int SerializedLength
    {
        get
        {
            var length = HeaderSize;
            foreach (var channel in _channels) length = checked(length + sizeof(int) + channel.Length);
            return length;
        }
    }
    /// <summary>Saves all channels and their shared depth into caller memory. Returns bytes written.</summary>
    public int CopyEncodedTo(Span<byte> destination)
    {
        var length = SerializedLength;
        if (destination.Length < length) throw new ArgumentException("Destination is too short.", nameof(destination));
        destination = destination[..length];
        foreach (var channel in _channels)
            if (destination.Overlaps(channel.Span)) throw new ArgumentException("Destination overlaps a channel encoding.");
        destination[0] = 0x4f; destination[1] = 0x43; destination[2] = 1; destination[3] = (byte)Levels;
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], ChannelCount);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], length);
        var offset = HeaderSize;
        foreach (var channel in _channels)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], channel.Length);
            offset += sizeof(int); channel.Span.CopyTo(destination[offset..]); offset += channel.Length;
        }
        return length;
    }
    /// <summary>Validates and loads a chunk packet without copying its channel payloads.</summary>
    /// <remarks>The exact packet memory must remain immutable and alive until this chunk and its users are released.</remarks>
    public static OctreeChunk FromEncoded(ReadOnlyMemory<byte> encoded)
    {
        var data = encoded.Span;
        if (data.Length < HeaderSize || data[0] != 0x4f || data[1] != 0x43 || data[2] != 1 ||
            data[3] > Octree.MaxLevels || BinaryPrimitives.ReadInt32LittleEndian(data[8..]) != data.Length)
            throw new FormatException("Invalid chunk packet header.");
        var count = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        if (count <= 0 || count > (data.Length - HeaderSize) / 7) throw new FormatException("Invalid channel count.");
        var offset = HeaderSize;
        // Validate before allocating descriptors for untrusted input.
        for (var i = 0; i < count; i++)
        {
            if (data.Length - offset < 4) throw new FormatException("Truncated channel header.");
            var length = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;
            if (length < 3 || length > data.Length - offset ||
                !OctreeSpan.TryCreate(data.Slice(offset, length), out var channel) || channel.Levels != data[3] || !channel.IsWellFormed())
                throw new FormatException("Invalid channel payload.");
            offset += length;
        }
        if (offset != data.Length) throw new FormatException("Trailing chunk packet data.");
        var channels = new ReadOnlyMemory<byte>[count]; offset = HeaderSize;
        for (var i = 0; i < count; i++)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]); offset += 4;
            channels[i] = encoded.Slice(offset, length); offset += length;
        }
        return new OctreeChunk(data[3], channels);
    }
}
