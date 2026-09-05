using System.Threading;

namespace Tedd.Voxtree.Tests;

public sealed class WorldConcurrencyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly VoxelBox Whole = new(0, 0, 0, 8, 8, 8);
    private static OctreeChunk Chunk(uint value) => OctreeChunk.FromDense(1, 2,
        Enumerable.Repeat(value, 16).ToArray());

    [Fact]
    public void ReadBatchesShareTheLockAndExcludeWriters()
    {
        using var world = new OctreeWorld(3, 1, 2, 1, initiallyEmpty: true);
        var chunk = Chunk(7);
        using var readerEntered = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        using var writerStarted = new ManualResetEventSlim();
        using var writerFinished = new ManualResetEventSlim();
        Worker reader, writer;
        using (world.BeginReadBatch())
        {
            reader = new Worker(() =>
            {
                using var batch = world.BeginReadBatch();
                Assert.Equal(0u, world.Get(0, 0, 0, 0));
                readerEntered.Set();
                Assert.True(releaseReader.Wait(Timeout));
            });
            try
            {
                Assert.True(readerEntered.Wait(Timeout));
                writer = new Worker(() =>
                {
                    writerStarted.Set();
                    world.LoadChunk(0, 0, 0, chunk);
                    writerFinished.Set();
                });
                Assert.True(writerStarted.Wait(Timeout));
                Assert.False(writerFinished.Wait(150));
                Assert.Equal(0UL, world.Revision);
            }
            finally { releaseReader.Set(); }
        }
        reader.Join(); writer.Join();
        Assert.True(writerFinished.IsSet);
        Assert.Equal(7u, world.Get(0, 0, 0, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteBatchExcludesOtherReadersAndWriters(bool competingWriter)
    {
        using var world = new OctreeWorld(3, 1, 2, 2, initiallyEmpty: true);
        var chunk = Chunk(9);
        using var started = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        Worker worker;
        using (world.BeginWriteBatch())
        {
            world.LoadChunk(0, 0, 0, chunk);
            worker = new Worker(() =>
            {
                started.Set();
                if (competingWriter) world.LoadChunk(1, 0, 0, chunk);
                else
                {
                    Assert.Equal(9u, world.Get(0, 0, 0, 0));
                    Assert.Equal(9u, world.Get(0, 2, 0, 0));
                }
                finished.Set();
            });
            Assert.True(started.Wait(Timeout));
            Assert.False(finished.Wait(150));
            world.LoadChunk(1, 0, 0, chunk);
            Assert.Equal(2, world.ResidentChunkCount);
        }
        worker.Join();
        Assert.True(finished.IsSet);
    }

    [Fact]
    public void NestedBatchesReuseLocksAndReadToWriteUpgradesFail()
    {
        using var world = new OctreeWorld(3, 1, 2, 1, initiallyEmpty: true);
        var chunk = Chunk(4);
        using (world.BeginReadBatch())
        {
            using var nested = world.BeginReadBatch();
            Assert.Equal(0u, world.Get(0, 0, 0, 0));
            Assert.Throws<LockRecursionException>(() => world.LoadChunk(0, 0, 0, chunk));
            Assert.Throws<LockRecursionException>(() => { using var invalid = world.BeginWriteBatch(); });
            Assert.Throws<LockRecursionException>(() => world.TrySetEmptyRegion(0, 0, 0, 3));
            Assert.Throws<LockRecursionException>(() => world.TryUnloadRegion(0, 0, 0, 3));
        }
        using (world.BeginWriteBatch())
        {
            using var nestedWrite = world.BeginWriteBatch();
            using var nestedRead = world.BeginReadBatch();
            world.LoadChunk(0, 0, 0, chunk);
            Assert.Equal(4u, world.Get(0, 0, 0, 0));
            Assert.True(world.TryGetChunk(0, 0, 0, out var retained));
            Assert.Same(chunk, retained);
        }
        new Worker(() => Assert.Equal(4u, world.Get(0, 0, 0, 0))).Join();
    }

    [Fact]
    public void BatchesReleaseOnExceptionsWithoutRollingBackSuccessfulWrites()
    {
        using var world = new OctreeWorld(3, 1, 2, 1, initiallyEmpty: true);
        var chunk = Chunk(6);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var batch = world.BeginWriteBatch();
            world.LoadChunk(0, 0, 0, chunk);
            world.LoadChunk(1, 0, 0, chunk); // Fixed capacity exhausted.
        });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var batch = world.BeginReadBatch();
            world.Get(2, 0, 0, 0);
        });
        new Worker(() =>
        {
            Assert.Equal(6u, world.Get(0, 0, 0, 0));
            Assert.Equal(1UL, world.Revision);
            Assert.True(world.TryUnloadRegion(0, 0, 0, 3));
        }).Join();
    }

    [Fact]
    public void ScopeDisposalIsIdempotentAndRejectsOutOfOrderDisposal()
    {
        using var world = new OctreeWorld(3, 1, 2, 0);
        var outer = world.BeginWriteBatch();
        var inner = world.BeginReadBatch();
        try
        {
            try { outer.Dispose(); Assert.Fail("Out-of-order disposal should fail."); }
            catch (SynchronizationLockException) { }
        }
        finally { inner.Dispose(); outer.Dispose(); }
        outer.Dispose(); inner.Dispose();
        OctreeWorld.Batch empty = default;
        empty.Dispose();
        new Worker(() => { using var batch = world.BeginWriteBatch(); }).Join();
    }

    [Fact]
    public void LocksAreIndependentBetweenWorlds()
    {
        using var first = new OctreeWorld(3, 1, 2, 0);
        using var second = new OctreeWorld(3, 1, 2, 0);
        using var batch = first.BeginWriteBatch();
        new Worker(() =>
        {
            using var other = second.BeginWriteBatch();
            Assert.True(second.TrySetEmptyRegion(0, 0, 0, 3));
        }).Join();
    }

    [Fact]
    public void ConcurrentQueriesSeeWholeMultiChunkAndMultiChannelBatches()
    {
        using var world = new OctreeWorld(3, 1, 2, 8, initiallyEmpty: true);
        var chunks = new[] { Chunk(1), Chunk(2) };
        using var start = new Barrier(4);
        var writer = new Worker(() =>
        {
            Assert.True(start.SignalAndWait(Timeout));
            for (var i = 0; i < 1000; i++)
            {
                using var batch = world.BeginWriteBatch();
                // Exercise branch collapse/reuse, known-empty and unloaded transitions.
                Assert.True(world.TryUnloadRegion(0, 0, 0, 3));
                Assert.True(world.TrySetEmptyRegion(0, 0, 0, 3));
                world.LoadChunk(0, 0, 0, chunks[i & 1]);
                world.LoadChunk(1, 0, 0, chunks[i & 1]);
            }
        });
        var readers = Enumerable.Range(0, 3).Select(_ => new Worker(() =>
        {
            var block = new uint[128];
            var regions = new OctreeWorldRegion[128];
            Assert.True(start.SignalAndWait(Timeout));
            for (var i = 0; i < 1000; i++)
            {
                using var batch = world.BeginReadBatch();
                var revision = world.Revision;
                var value = world.Get(0, 0, 0, 0);
                Assert.Equal(value, world.Get(1, 2, 0, 0));
                Assert.True(world.TryCountMatches(Whole, 0, VoxelFilter.NonZero, out var count));
                Assert.Equal(value == 0 ? 0L : 16L, count);
                Assert.Equal(value != 0, world.Any(Whole, 1, VoxelFilter.NonZero));
                Assert.Equal(value == 0, world.IsKnownEmpty(Whole));
                world.CopyBlockTo(0, 0, 0, 2, block);
                Assert.Equal(value, block[0]); Assert.Equal(value, block[32]);
                Assert.True(block.AsSpan(0, 64).SequenceEqual(block.AsSpan(64)));
                world.CopyChannelBlockTo(1, 0, 0, 0, 2, block);
                Assert.Equal(value, block[0]); Assert.Equal(value, block[32]);
                world.CopyRegionTo(0, new VoxelBox(0, 0, 0, 4, 2, 2), block);
                for (var j = 0; j < 16; j++) Assert.Equal(value, block[j]);
                Assert.True(world.QueryRegions(Whole, regions, out var written));
                Assert.Equal(1 + 7 * world.BranchCount, written);
                Assert.Equal(value == 0 ? 0 : 2, world.ResidentChunkCount);
                Assert.Equal(value == 0 ? OctreeRegionKind.Empty : OctreeRegionKind.Loaded,
                    world.GetRegion(0, 0, 0).Kind);
                Assert.Equal(revision, world.Revision);
            }
        })).ToArray();
        writer.Join(); foreach (var reader in readers) reader.Join();
    }

    [Fact]
    public void IndividualBulkReadsRemainConsistentDuringPublication()
    {
        using var world = new OctreeWorld(2, 1, 2, 8, initiallyEmpty: true);
        var chunks = new[] { Chunk(3), Chunk(5) };
        using var start = new Barrier(2);
        var writer = new Worker(() =>
        {
            Assert.True(start.SignalAndWait(Timeout));
            for (var i = 0; i < 1000; i++)
            {
                using var batch = world.BeginWriteBatch();
                for (var x = 0; x < 2; x++)
                for (var y = 0; y < 2; y++)
                for (var z = 0; z < 2; z++) world.LoadChunk(x, y, z, chunks[i & 1]);
            }
        });
        var reader = new Worker(() =>
        {
            var output = new uint[128];
            Assert.True(start.SignalAndWait(Timeout));
            for (var i = 0; i < 1000; i++)
            {
                world.CopyBlockTo(0, 0, 0, 2, output);
                Assert.All(output, v => Assert.Equal(output[0], v));
                Assert.True(world.TryCountMatches(new VoxelBox(0, 0, 0, 4, 4, 4), 0,
                    VoxelFilter.EqualTo(3), out var count));
                Assert.True(count == 0 || count == 64);
            }
        });
        writer.Join(); reader.Join();
    }

    [Fact]
    public void ConcurrentWritersPreserveCapacityRevisionAndRetainedSnapshots()
    {
        using var world = new OctreeWorld(3, 1, 2, 4, initiallyEmpty: true);
        var original = Chunk(7); var replacement = Chunk(8);
        world.LoadChunk(0, 0, 0, original);
        var retained = world.GetRegion(0, 0, 0);
        Assert.True(world.TryUnloadRegion(0, 0, 0, 3));
        var initialRevision = world.Revision;
        using var start = new Barrier(4);
        var workers = Enumerable.Range(0, 4).Select(x => new Worker(() =>
        {
            Assert.True(start.SignalAndWait(Timeout));
            for (var i = 0; i < 500; i++)
            {
                world.LoadChunk(x, 0, 0, original);
                Assert.True(world.TryLoadRegion(new OctreeWorldRegion(x * 2, 0, 0, 1,
                    OctreeRegionKind.Loaded, replacement)));
                Assert.True(world.TrySetEmptyRegion(x * 2, 0, 0, 1));
                Assert.True(world.TryUnloadRegion(x * 2, 0, 0, 1));
            }
        })).ToArray();
        foreach (var worker in workers) worker.Join();
        Assert.Equal(initialRevision + 4UL * 500 * 4, world.Revision);
        Assert.Equal(0, world.ResidentChunkCount);
        Assert.Equal(0, world.BranchCount);
        Assert.Equal(7u, retained.Chunk!.GetChannel(0).Get(0, 0, 0));
    }

    [Fact]
    public void WarmBatchesAndOperationsAllocateNoManagedMemory()
    {
        using var world = new OctreeWorld(3, 1, 2, 1, initiallyEmpty: true);
        var chunk = Chunk(2);
        for (var i = 0; i < 100; i++) ExerciseBatches(world, chunk);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) ExerciseBatches(world, chunk);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void ExerciseBatches(OctreeWorld world, OctreeChunk chunk)
    {
        using (world.BeginWriteBatch())
        {
            using var nested = world.BeginWriteBatch();
            world.LoadChunk(0, 0, 0, chunk);
            _ = world.Get(0, 0, 0, 0);
            _ = world.TrySetEmptyRegion(0, 0, 0, 3);
        }
        using (world.BeginReadBatch())
        {
            using var nested = world.BeginReadBatch();
            _ = world.Get(0, 0, 0, 0);
            _ = world.Revision;
        }
    }

    [Fact]
    public void DisposedWorldRejectsOperationsAndBatches()
    {
        var world = new OctreeWorld(3, 1, 2, 1);
        world.Dispose(); world.Dispose();
        Assert.Throws<ObjectDisposedException>(() => world.Get(0, 0, 0, 0));
        Assert.Throws<ObjectDisposedException>(() => world.TryUnloadRegion(0, 0, 0, 3));
        Assert.Throws<ObjectDisposedException>(() => { using var batch = world.BeginReadBatch(); });
        Assert.Throws<ObjectDisposedException>(() => { using var batch = world.BeginWriteBatch(); });
    }

    private sealed class Worker
    {
        private readonly Thread _thread;
        private Exception? _error;
        internal Worker(Action action)
        {
            _thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception error) { _error = error; }
            }) { IsBackground = true };
            _thread.Start();
        }
        internal void Join()
        {
            Assert.True(_thread.Join(Timeout), "Worker did not complete; possible deadlock.");
            if (_error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_error).Throw();
        }
    }
}
