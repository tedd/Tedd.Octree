using System;

namespace Tedd.Voxtree;

public sealed partial class OctreeWorld
{
    /// <summary>Creates a mutable Morton-order editor from a loaded or known-empty chunk.</summary>
    /// <remarks>
    /// The immutable world state remains visible until commit. False means the chunk is unloaded.
    /// The returned editor is exclusively owned and may be modified without holding a world lock.
    /// </remarks>
    public bool TryMarkChunkHot(int chunkX, int chunkY, int chunkZ, out HotOctreeChunk? hotChunk)
    {
        OctreeChunk? source;
        bool knownEmpty;
        using (ReadLock())
        {
            ChunkOrigin(chunkX, chunkY, chunkZ, out var x, out var y, out var z);
            var link = Find(x, y, z, out _);
            if (link == 0)
            {
                hotChunk = null;
                return false;
            }
            knownEmpty = link == -1;
            source = link < -1 ? _chunks.Span[-link - 2] : null;
        }
        hotChunk = knownEmpty
            ? HotOctreeChunk.FromKnownEmpty(ChunkLevels, ChannelCount)
            : source!.MarkHot();
        return true;
    }

    /// <summary>Creates a mutable Morton-order editor, throwing when the chunk is unloaded.</summary>
    public HotOctreeChunk MarkChunkHot(int chunkX, int chunkY, int chunkZ)
    {
        if (TryMarkChunkHot(chunkX, chunkY, chunkZ, out var hotChunk)) return hotChunk!;
        throw new InvalidOperationException("The requested chunk is unloaded.");
    }

    /// <summary>Re-encodes and atomically publishes a hot chunk when its source is still current.</summary>
    /// <remarks>
    /// Encoding occurs before the write lock is acquired. False indicates a stale source or
    /// insufficient fixed world capacity; the editor remains hot and retains all modifications.
    /// </remarks>
    public bool TryCommitHotChunk(int chunkX, int chunkY, int chunkZ, HotOctreeChunk hotChunk)
    {
        ValidateHotChunk(hotChunk);
        var snapshot = hotChunk.BuildSnapshot();
        using var scope = WriteLock();
        ChunkOrigin(chunkX, chunkY, chunkZ, out var x, out var y, out var z);
        var link = Find(x, y, z, out _);
        var current = hotChunk.SourceWasKnownEmpty
            ? link == -1
            : link < -1 && ReferenceEquals(_chunks.Span[-link - 2], hotChunk.SourceChunk);
        if (!current || !TryLoadChunk(chunkX, chunkY, chunkZ, snapshot)) return false;
        hotChunk.Complete(snapshot);
        return true;
    }

    /// <summary>Re-encodes and atomically publishes a hot chunk.</summary>
    public OctreeChunk CommitHotChunk(int chunkX, int chunkY, int chunkZ, HotOctreeChunk hotChunk)
    {
        if (TryCommitHotChunk(chunkX, chunkY, chunkZ, hotChunk)) return hotChunk.CommittedChunk!;
        throw new InvalidOperationException("The hot chunk source is stale or world capacity is exhausted.");
    }

    /// <summary>Equivalent to <see cref="CommitHotChunk"/>.</summary>
    public OctreeChunk UnmarkChunkHot(int chunkX, int chunkY, int chunkZ, HotOctreeChunk hotChunk) =>
        CommitHotChunk(chunkX, chunkY, chunkZ, hotChunk);

    private void ValidateHotChunk(HotOctreeChunk hotChunk)
    {
        if (hotChunk is null) throw new ArgumentNullException(nameof(hotChunk));
        if (hotChunk.Levels != ChunkLevels || hotChunk.ChannelCount != ChannelCount)
            throw new ArgumentException("Hot chunk schema does not match the world.", nameof(hotChunk));
    }
}
