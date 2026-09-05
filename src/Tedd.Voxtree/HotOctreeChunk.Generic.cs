using System;

namespace Tedd.Voxtree;

/// <summary>A mutable, Morton-ordered dense generic chunk for edit-intensive workloads.</summary>
/// <remarks>
/// A hot chunk is exclusively owned and is not thread-safe. Worlds retain the immutable source
/// snapshot until commit publishes the re-encoded result under the world's write lock.
/// </remarks>
public sealed class HotOctreeChunk<T> where T : unmanaged
{
    private T[]? _values;
    private OctreeChunk<T>? _committed;

    private HotOctreeChunk(int levels, int channelCount, T[] values,
        OctreeChunk<T>? sourceChunk, bool sourceWasKnownEmpty)
    {
        VoxelType<T>.Validate();
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
    public OctreeChunk<T>? CommittedChunk => _committed;

    internal OctreeChunk<T>? SourceChunk { get; }
    internal bool SourceWasKnownEmpty { get; }

    /// <summary>Creates a mutable Morton-ordered copy of an immutable chunk.</summary>
    public static HotOctreeChunk<T> FromChunk(OctreeChunk<T> chunk)
    {
        if (chunk is null) throw new ArgumentNullException(nameof(chunk));
        var values = new T[checked(OctreeCodec.GetVoxelCount(chunk.Levels) * chunk.ChannelCount)];
        chunk.CopyBlockTo(0, 0, 0, chunk.Levels, values, DenseVoxelLayout.Morton);
        return new HotOctreeChunk<T>(chunk.Levels, chunk.ChannelCount, values, chunk, false);
    }

    /// <summary>Creates a mutable all-zero chunk in Morton order.</summary>
    public static HotOctreeChunk<T> Empty(int levels, int channelCount)
    {
        VoxelType<T>.Validate();
        OctreeCodec.ValidateLevels(levels);
        if (channelCount <= 0) throw new ArgumentOutOfRangeException(nameof(channelCount));
        return new HotOctreeChunk<T>(levels, channelCount,
            new T[checked(OctreeCodec.GetVoxelCount(levels) * channelCount)], null, false);
    }

    internal static HotOctreeChunk<T> FromKnownEmpty(int levels, int channelCount) =>
        new(levels, channelCount,
            new T[checked(OctreeCodec.GetVoxelCount(levels) * channelCount)], null, true);

    /// <summary>Reads or writes a chunk-local voxel.</summary>
    public T this[int channel, int x, int y, int z]
    {
        get => GetChannelSpan(channel)[DenseVoxel.GetIndex(x, y, z, Levels, DenseVoxelLayout.Morton)];
        set => GetChannelSpan(channel)[DenseVoxel.GetIndex(x, y, z, Levels, DenseVoxelLayout.Morton)] = value;
    }

    /// <summary>Returns the mutable Morton-ordered slice for one channel.</summary>
    public Span<T> GetChannelSpan(int channel)
    {
        var values = GetValues();
        if ((uint)channel >= (uint)ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
        var count = OctreeCodec.GetVoxelCount(Levels);
        return values.AsSpan(channel * count, count);
    }

    /// <summary>Returns a mutable multi-channel Morton-order view.</summary>
    public DenseVoxelBlockSpan<T> AsSpan() =>
        new(GetValues(), Levels, ChannelCount, DenseVoxelLayout.Morton);

    /// <summary>Encodes the dense channels into an immutable octree chunk and closes this editor.</summary>
    public OctreeChunk<T> Commit()
    {
        if (_committed is not null) return _committed;
        var chunk = BuildSnapshot();
        Complete(chunk);
        return chunk;
    }

    /// <summary>Equivalent to <see cref="Commit"/>.</summary>
    public OctreeChunk<T> UnmarkHot() => Commit();

    internal OctreeChunk<T> BuildSnapshot() => _committed ??
        OctreeChunk<T>.FromDense(Levels, ChannelCount, GetValues(), DenseVoxelLayout.Morton);

    internal void Complete(OctreeChunk<T> chunk)
    {
        _committed = chunk;
        _values = null;
    }

    private T[] GetValues() => _values ??
        throw new InvalidOperationException("The hot chunk has already been committed.");
}
