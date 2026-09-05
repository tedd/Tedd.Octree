using System;

namespace Tedd.Voxtree;

public sealed partial class WorldEntity
{
    /// <summary>Creates a mutable Morton-order editor from a loaded chunk coordinate.</summary>
    public bool TryMarkChunkHot(ChunkCoordinate coordinate, out HotOctreeChunk? hotChunk)
    {
        if (!_chunks.TryGetValue(coordinate, out var chunk))
        {
            hotChunk = null;
            return false;
        }
        hotChunk = chunk!.MarkHot();
        return true;
    }

    /// <summary>Creates a mutable Morton-order editor from a loaded chunk coordinate.</summary>
    public bool TryMarkChunkHot(long chunkX, long chunkY, long chunkZ, out HotOctreeChunk? hotChunk) =>
        TryMarkChunkHot(new ChunkCoordinate(chunkX, chunkY, chunkZ), out hotChunk);

    /// <summary>Creates a mutable Morton-order editor, throwing when the chunk is not loaded.</summary>
    public HotOctreeChunk MarkChunkHot(ChunkCoordinate coordinate)
    {
        if (TryMarkChunkHot(coordinate, out var hotChunk)) return hotChunk!;
        throw new InvalidOperationException("The requested chunk is not loaded.");
    }

    /// <summary>Creates a mutable Morton-order editor, throwing when the chunk is not loaded.</summary>
    public HotOctreeChunk MarkChunkHot(long chunkX, long chunkY, long chunkZ) =>
        MarkChunkHot(new ChunkCoordinate(chunkX, chunkY, chunkZ));

    /// <summary>Re-encodes and replaces a chunk when the editor's source is still current.</summary>
    public bool TryCommitHotChunk(ChunkCoordinate coordinate, HotOctreeChunk hotChunk)
    {
        ValidateHotChunk(hotChunk);
        var snapshot = hotChunk.BuildSnapshot();
        if (!_chunks.TryGetValue(coordinate, out var current) ||
            !ReferenceEquals(current, hotChunk.SourceChunk)) return false;
        _chunks.Set(coordinate, snapshot, snapshot.SerializedLength, isDirty: true);
        hotChunk.Complete(snapshot);
        return true;
    }

    /// <summary>Re-encodes and replaces a chunk when the editor's source is still current.</summary>
    public bool TryCommitHotChunk(long chunkX, long chunkY, long chunkZ, HotOctreeChunk hotChunk) =>
        TryCommitHotChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), hotChunk);

    /// <summary>Re-encodes and replaces a chunk, throwing when its source is stale.</summary>
    public OctreeChunk CommitHotChunk(ChunkCoordinate coordinate, HotOctreeChunk hotChunk)
    {
        if (TryCommitHotChunk(coordinate, hotChunk)) return hotChunk.CommittedChunk!;
        throw new InvalidOperationException("The hot chunk source is stale.");
    }

    /// <summary>Re-encodes and replaces a chunk, throwing when its source is stale.</summary>
    public OctreeChunk CommitHotChunk(long chunkX, long chunkY, long chunkZ, HotOctreeChunk hotChunk) =>
        CommitHotChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), hotChunk);

    /// <summary>Equivalent to <see cref="CommitHotChunk(ChunkCoordinate, HotOctreeChunk)"/>.</summary>
    public OctreeChunk UnmarkChunkHot(ChunkCoordinate coordinate, HotOctreeChunk hotChunk) =>
        CommitHotChunk(coordinate, hotChunk);

    private void ValidateHotChunk(HotOctreeChunk hotChunk)
    {
        if (hotChunk is null) throw new ArgumentNullException(nameof(hotChunk));
        if (hotChunk.SideLength != ChunkSize || hotChunk.ChannelCount != ChannelCount)
            throw new ArgumentException("Hot chunk schema does not match the world entity.", nameof(hotChunk));
    }
}
