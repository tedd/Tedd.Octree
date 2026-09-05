using System.Runtime.InteropServices;
using Tedd.Voxtree;
using Model = global::Tedd.Voxtree.Octree;

namespace Tedd.Voxtree.Tests;

public class NeighborhoodCacheTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MovingCacheMatchesSourceAcrossWrapsTeleportsAndEdges(int pattern)
    {
        var source = SpatialQueryTests.CreateValues(pattern, 16);
        var tree = new Model(4, source);
        var cache = new OctreeNeighborhoodCache(4);
        var random = new Random(42);
        for (var iteration = 0; iteration < 80; iteration++)
        {
            var x = iteration < 16 ? iteration : random.Next(16);
            var y = iteration < 16 ? 8 : random.Next(16);
            var z = iteration < 16 ? 8 : random.Next(16);
            var previous = cache.Bounds;
            var copied = cache.Update(tree, x, y, z);
            var box = cache.Bounds;
            var reused = iteration == 0 ? 0 : IntersectionCount(previous, box);
            Assert.Equal(64 - reused, copied);
            for (var xx = box.MinX; xx < box.MaxX; xx++)
            for (var yy = box.MinY; yy < box.MaxY; yy++)
            for (var zz = box.MinZ; zz < box.MaxZ; zz++)
            {
                Assert.Equal(tree.Get(xx, yy, zz), cache.Get(xx, yy, zz));
                Assert.True(cache.TryGet(xx, yy, zz, out var value));
                Assert.Equal(tree.Get(xx, yy, zz), value);
            }
            Assert.Equal(tree.Any(box, VoxelFilter.NonZero), cache.Any(box, VoxelFilter.NonZero));
            Assert.Equal(tree.Any(box, VoxelFilter.EqualTo(0)), cache.Any(box, VoxelFilter.EqualTo(0)));
            Assert.Equal(0, cache.Update(tree, x, y, z));
        }
        Assert.False(cache.TryGet(-1, 0, 0, out _));
    }

    [Fact]
    public void OwnedRebuildAndSourceReplacementRefreshTheCompleteCache()
    {
        var values = new uint[512];
        var tree = new Model(3, values);
        var cache = new OctreeNeighborhoodCache(4);
        Assert.Equal(64, cache.Update(tree, 4, 4, 4));
        values[(4 * 8 + 4) * 8 + 4] = 19;
        tree.Build(values);
        Assert.Equal(64, cache.Update(tree, 4, 4, 4));
        Assert.Equal(19u, cache.Get(4, 4, 4));
        Assert.Equal(64, cache.Update(new Model(3, new uint[512]), 4, 4, 4));
        Assert.Equal(0u, cache.Get(4, 4, 4));
        cache.Invalidate(new VoxelBox(0, 0, 0, 1, 1, 1));
        Assert.True(cache.IsValid);
        cache.Invalidate(new VoxelBox(4, 4, 4, 5, 5, 5));
        Assert.False(cache.IsValid);
        Assert.Throws<InvalidOperationException>(() => cache.Get(4, 4, 4));
        Assert.Equal(64, cache.Update(tree, 4, 4, 4));
    }

    [Fact]
    public void BorrowedBufferRevisionAndExplicitInvalidationPreventStaleReads()
    {
        var values = new uint[512];
        var storage = new byte[Model.GetMaximumSize(3)];
        var length = Model.Build(values, 3, storage);
        Span<uint> cacheStorage = stackalloc uint[64];
        var cache = new OctreeNeighborhoodSpan(cacheStorage, 4);
        Assert.Equal(64, cache.Update(new OctreeSpan(storage.AsSpan(0, length)), 1, 4, 4, 4));
        Array.Fill(values, 7u); // Same encoded length and address; only revision distinguishes this build.
        length = Model.Build(values, 3, storage);
        Assert.Equal(64, cache.Update(new OctreeSpan(storage.AsSpan(0, length)), 2, 4, 4, 4));
        Assert.Equal(7u, cache.Get(4, 4, 4));
        cache.Invalidate();
        Assert.False(cache.TryGet(4, 4, 4, out _));
        Assert.Equal(64, cache.Update(new OctreeSpan(storage.AsSpan(0, length)), 2, 4, 4, 4));
    }

    [Fact]
    public void CacheRejectsOverlapInvalidCentersAndInsufficientStorage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OctreeNeighborhoodCache(3));
        Assert.Throws<ArgumentException>(() => new OctreeNeighborhoodCache(new uint[63], 4));
        var cache = new OctreeNeighborhoodCache(4);
        Assert.Throws<ArgumentException>(() => cache.Update(new Model(1, new uint[8]), 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => cache.Update(new Model(3, new uint[512]), -1, 0, 0));
        Assert.Throws<InvalidOperationException>(() => cache.Update(new Model(3), 0, 0, 0));
        var bytes = new byte[512];
        var written = Model.Build(new uint[512], 3, bytes);
        var snapshot = (byte[])bytes.Clone();
        Assert.Throws<ArgumentException>(() => UpdateOverlapping(bytes, written));
        Assert.Equal(snapshot, bytes);
    }

    [Fact]
    public void FailedPartialRefreshInvalidatesBorrowedCacheUntilRecovery()
    {
        Span<uint> storage = stackalloc uint[512];
        var cache = new OctreeNeighborhoodSpan(storage, 8);
        var good = new Model(3, Enumerable.Repeat(7u, 512).ToArray());
        Assert.Equal(512, cache.Update(good, 4, 4, 4));

        // Seven valid leaves followed by an invalid zero child distance. Header remains valid.
        var malformed = new byte[] { 0x4f, 0x4c, 0x7f, 1, 1, 1, 1, 1, 1, 1, 0, 9 };
        var view = new OctreeSpan(malformed);
        Assert.False(view.IsWellFormed());
        var failed = false;
        try { cache.Update(view, 1, 4, 4, 4); }
        catch (FormatException) { failed = true; }
        Assert.True(failed);
        Assert.False(cache.IsValid);
        Assert.False(cache.TryGet(0, 0, 0, out _));
        Assert.Equal(512, cache.Update(good, 4, 4, 4));
        Assert.Equal(7u, cache.Get(0, 0, 0));
        Assert.Equal(7u, cache.Get(7, 7, 7));
    }

    [Fact]
    public void SingleVoxelCacheAndEmptyQueriesRespectLifecycle()
    {
        var cache = new OctreeNeighborhoodCache(new uint[1], 1);
        Assert.False(cache.IsValid);
        Assert.False(cache.TryGet(0, 0, 0, out _));
        var tree = new Model(0, new uint[] { uint.MaxValue });
        Assert.Equal(1, cache.Update(tree, 0, 0, 0));
        Assert.True(cache.Any(cache.Bounds, VoxelFilter.Masked(0x80000000, uint.MaxValue)));
        Assert.False(cache.Any(default, VoxelFilter.Any));
        Assert.Equal(0, cache.Update(tree, 0, 0, 0));
        cache.Invalidate(default);
        Assert.True(cache.IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => cache.Any(new VoxelBox(0, 0, 0, 2, 1, 1), default));
    }

    [Fact]
    public void SpanCacheUpdatesAndReadsAllocateZero()
    {
        var tree = new Model(4, SpatialQueryTests.CreateValues(3, 16));
        Span<uint> storage = stackalloc uint[64];
        var cache = new OctreeNeighborhoodSpan(storage, 4);
        for (var i = 0; i < 100; i++)
        {
            cache.Update(tree, 4 + (i & 1), 4, 4);
            _ = cache.Any(cache.Bounds, VoxelFilter.NonZero);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            cache.Update(tree, 4 + (i & 1), 4, 4);
            _ = cache.Get(4, 4, 4);
            _ = cache.Any(cache.Bounds, VoxelFilter.NonZero);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void QueryAndCacheOperationsAllocateZeroAfterProvisioning()
    {
        var tree = new Model(4, SpatialQueryTests.CreateValues(3, 16));
        var cache = new OctreeNeighborhoodCache(4);
        var hits = new VoxelHit[64];
        var dense = new uint[64];
        Exercise(tree, cache, hits, dense, 100);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Exercise(tree, cache, hits, dense, 500);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void Exercise(Model tree, OctreeNeighborhoodCache cache, VoxelHit[] hits, uint[] dense, int count)
    {
        for (var i = 0; i < count; i++)
        {
            cache.Update(tree, 4 + (i & 1), 4, 4);
            var box = cache.Bounds;
            _ = cache.Any(box, VoxelFilter.NonZero);
            _ = cache.Get(4, 4, 4);
            _ = tree.Any(box, VoxelFilter.NonZero);
            _ = tree.CountMatches(box, VoxelFilter.NonZero);
            _ = tree.Query(box, VoxelFilter.NonZero, hits, out _);
            tree.CopyRegionTo(box, dense);
            _ = tree.TryFindNearest(4, 4, 4, 3, VoxelFilter.NonZero, out _);
        }
    }

    private static void UpdateOverlapping(byte[] bytes, int written)
    {
        var cache = new OctreeNeighborhoodSpan(MemoryMarshal.Cast<byte, uint>(bytes.AsSpan(0, 256)), 4);
        cache.Update(new OctreeSpan(bytes.AsSpan(0, written)), 0, 4, 4, 4);
    }

    private static int IntersectionCount(VoxelBox a, VoxelBox b) =>
        Math.Max(0, Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX)) *
        Math.Max(0, Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY)) *
        Math.Max(0, Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ));
}
