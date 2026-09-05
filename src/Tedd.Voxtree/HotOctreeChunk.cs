using System;

namespace Tedd.Voxtree;

/// <summary>A mutable, Morton-ordered dense chunk for edit-intensive workloads.</summary>
/// <remarks>
/// A hot chunk is exclusively owned and is not thread-safe. Worlds continue exposing the
/// immutable source snapshot until this editor is committed back to them. Commit encodes
/// every channel once and closes the editor; the resulting <see cref="OctreeChunk"/> is immutable.
/// </remarks>
public sealed class HotOctreeChunk
{
    private uint[]? _values;
    private OctreeChunk? _committed;

    private HotOctreeChunk(int levels, int channelCount, uint[] values,
        OctreeChunk? sourceChunk, bool sourceWasKnownEmpty)
    {
        Levels = levels;
        ChannelCount = channelCount;
        _values = values;
        SourceChunk = sourceChunk;
        SourceWasKnownEmpty = sourceWasKnownEmpty;
    }

    /// <summary>The chunk depth.</summary>
    public int Levels { get; }
    /// <summary>The chunk side length.</summary>
    public int SideLength => 1 << Levels;
    /// <summary>The channel count.</summary>
    public int ChannelCount { get; }
    /// <summary>The fixed dense storage order.</summary>
    public DenseVoxelLayout Layout => DenseVoxelLayout.Morton;
    /// <summary>Whether this editor still owns mutable dense storage.</summary>
    public bool IsHot => _values is not null;
    /// <summary>The immutable result after commit; null while the chunk remains hot.</summary>
    public OctreeChunk? CommittedChunk => _committed;

    internal OctreeChunk? SourceChunk { get; }
    internal bool SourceWasKnownEmpty { get; }

    /// <summary>Creates a mutable Morton-ordered copy of an immutable chunk.</summary>
    public static HotOctreeChunk FromChunk(OctreeChunk chunk)
    {
        if (chunk is null) throw new ArgumentNullException(nameof(chunk));
        var values = new uint[checked(OctreeCodec.GetVoxelCount(chunk.Levels) * chunk.ChannelCount)];
        chunk.CopyBlockTo(0, 0, 0, chunk.Levels, values, DenseVoxelLayout.Morton);
        return new HotOctreeChunk(chunk.Levels, chunk.ChannelCount, values, chunk, false);
    }

    /// <summary>Creates a mutable all-zero chunk in Morton order.</summary>
    public static HotOctreeChunk Empty(int levels, int channelCount)
    {
        OctreeCodec.ValidateLevels(levels);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        return new HotOctreeChunk(levels, channelCount,
            new uint[checked(OctreeCodec.GetVoxelCount(levels) * channelCount)], null, false);
    }

    internal static HotOctreeChunk FromKnownEmpty(int levels, int channelCount) =>
        new(levels, channelCount,
            new uint[checked(OctreeCodec.GetVoxelCount(levels) * channelCount)], null, true);

    /// <summary>Reads or writes a chunk-local voxel.</summary>
    public uint this[int channel, int x, int y, int z]
    {
        get => GetChannelSpan(channel)[DenseVoxel.GetIndex(x, y, z, Levels, DenseVoxelLayout.Morton)];
        set => GetChannelSpan(channel)[DenseVoxel.GetIndex(x, y, z, Levels, DenseVoxelLayout.Morton)] = value;
    }

    /// <summary>Returns the mutable Morton-ordered slice for one channel.</summary>
    public Span<uint> GetChannelSpan(int channel)
    {
        var values = GetValues();
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
        var count = OctreeCodec.GetVoxelCount(Levels);
        return values.AsSpan(channel * count, count);
    }

    /// <summary>Returns a mutable multi-channel Morton-order view.</summary>
    public DenseVoxelBlockSpan AsSpan() =>
        new(GetValues(), Levels, ChannelCount, DenseVoxelLayout.Morton);

    /// <summary>Encodes the dense channels into an immutable octree chunk and closes this editor.</summary>
    public OctreeChunk Commit()
    {
        if (_committed is not null) return _committed;
        var chunk = BuildSnapshot();
        Complete(chunk);
        return chunk;
    }

    /// <summary>Equivalent to <see cref="Commit"/>.</summary>
    public OctreeChunk UnmarkHot() => Commit();

    internal OctreeChunk BuildSnapshot() => _committed ??
        OctreeChunk.FromDense(Levels, ChannelCount, GetValues(), DenseVoxelLayout.Morton);

    internal void Complete(OctreeChunk chunk)
    {
        _committed = chunk;
        _values = null;
    }

    private uint[] GetValues() => _values ??
        throw new InvalidOperationException("The hot chunk has already been committed.");
}
