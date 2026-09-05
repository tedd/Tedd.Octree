using System;

namespace Tedd.Voxtree;

public sealed partial class WorldEntity<T> where T : unmanaged
{
    /// <summary>Creates a mutable Morton-order editor from a loaded chunk coordinate.</summary>
    public bool TryMarkChunkHot(ChunkCoordinate coordinate, out HotOctreeChunk<T>? hotChunk)
        => TryMarkChunkHot(new ChunkAddress(0, coordinate), out hotChunk);

    /// <summary>Creates a mutable Morton-order editor from a base or LOD chunk, loading it on demand.</summary>
    public bool TryMarkChunkHot(ChunkAddress address, out HotOctreeChunk<T>? hotChunk)
    {
        ValidateChunkAddress(address);
        if (!_chunks.TryGetValue(address, out var chunk) &&
            (StorageOptions is null || !TryGetOrLoadChunk(address, out chunk)))
        {
            hotChunk = null;
            return false;
        }
        hotChunk = chunk!.MarkHot();
        return true;
    }

    /// <summary>Creates a mutable Morton-order editor from a loaded chunk coordinate.</summary>
    public bool TryMarkChunkHot(long chunkX, long chunkY, long chunkZ, out HotOctreeChunk<T>? hotChunk) =>
        TryMarkChunkHot(new ChunkCoordinate(chunkX, chunkY, chunkZ), out hotChunk);

    /// <summary>Creates a mutable Morton-order editor, throwing when the chunk is not loaded.</summary>
    public HotOctreeChunk<T> MarkChunkHot(ChunkCoordinate coordinate)
        => MarkChunkHot(new ChunkAddress(0, coordinate));

    /// <summary>Creates a mutable Morton-order editor for a base or LOD chunk, loading it on demand.</summary>
    public HotOctreeChunk<T> MarkChunkHot(ChunkAddress address)
    {
        if (TryMarkChunkHot(address, out var hotChunk)) return hotChunk!;
        throw new InvalidOperationException("The requested chunk is not loaded.");
    }

    /// <summary>Creates a mutable Morton-order editor, throwing when the chunk is not loaded.</summary>
    public HotOctreeChunk<T> MarkChunkHot(long chunkX, long chunkY, long chunkZ) =>
        MarkChunkHot(new ChunkCoordinate(chunkX, chunkY, chunkZ));

    /// <summary>Re-encodes and replaces a chunk when the editor's source is still current.</summary>
    public bool TryCommitHotChunk(ChunkCoordinate coordinate, HotOctreeChunk<T> hotChunk)
        => TryCommitHotChunk(new ChunkAddress(0, coordinate), hotChunk);

    /// <summary>Re-encodes and replaces a base or LOD chunk when its source remains current.</summary>
    public bool TryCommitHotChunk(ChunkAddress address, HotOctreeChunk<T> hotChunk)
    {
        ValidateChunkAddress(address);
        ValidateHotChunk(hotChunk);
        var snapshot = hotChunk.BuildSnapshot();
        if (!_chunks.TryGetValue(address, out var current) ||
            !ReferenceEquals(current, hotChunk.SourceChunk)) return false;
        _chunks.Set(address, snapshot, snapshot.SerializedLength, isDirty: true);
        hotChunk.Complete(snapshot);
        return true;
    }

    /// <summary>Re-encodes and replaces a chunk when the editor's source is still current.</summary>
    public bool TryCommitHotChunk(long chunkX, long chunkY, long chunkZ, HotOctreeChunk<T> hotChunk) =>
        TryCommitHotChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), hotChunk);

    /// <summary>Re-encodes and replaces a chunk, throwing when its source is stale.</summary>
    public OctreeChunk<T> CommitHotChunk(ChunkCoordinate coordinate, HotOctreeChunk<T> hotChunk)
        => CommitHotChunk(new ChunkAddress(0, coordinate), hotChunk);

    /// <summary>Re-encodes and replaces a base or LOD chunk, throwing when its source is stale.</summary>
    public OctreeChunk<T> CommitHotChunk(ChunkAddress address, HotOctreeChunk<T> hotChunk)
    {
        if (TryCommitHotChunk(address, hotChunk)) return hotChunk.CommittedChunk!;
        throw new InvalidOperationException("The hot chunk source is stale.");
    }

    /// <summary>Re-encodes and replaces a chunk, throwing when its source is stale.</summary>
    public OctreeChunk<T> CommitHotChunk(long chunkX, long chunkY, long chunkZ, HotOctreeChunk<T> hotChunk) =>
        CommitHotChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), hotChunk);

    /// <summary>Equivalent to <see cref="CommitHotChunk(ChunkCoordinate, HotOctreeChunk{T})"/>.</summary>
    public OctreeChunk<T> UnmarkChunkHot(ChunkCoordinate coordinate, HotOctreeChunk<T> hotChunk) =>
        CommitHotChunk(coordinate, hotChunk);

    /// <summary>Equivalent to <see cref="CommitHotChunk(ChunkAddress, HotOctreeChunk{T})"/>.</summary>
    public OctreeChunk<T> UnmarkChunkHot(ChunkAddress address, HotOctreeChunk<T> hotChunk) =>
        CommitHotChunk(address, hotChunk);

    private void ValidateHotChunk(HotOctreeChunk<T> hotChunk)
    {
        if (hotChunk is null) throw new ArgumentNullException(nameof(hotChunk));
        if (hotChunk.SideLength != ChunkSize || hotChunk.ChannelCount != ChannelCount)
            throw new ArgumentException("Hot chunk schema does not match the world entity.", nameof(hotChunk));
    }
}
