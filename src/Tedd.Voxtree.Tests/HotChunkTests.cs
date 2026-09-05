namespace Tedd.Voxtree.Tests;

public sealed class HotChunkTests
{
    [Fact]
    public void HotChunkUsesMutableMortonStorageAndCommitsOnce()
    {
        const int levels = 2, channels = 2, count = 64;
        var values = new uint[count * channels];
        values[(1 * 4 + 2) * 4 + 3] = 17;
        values[count + (3 * 4 + 1) * 4 + 2] = 23;
        var source = OctreeChunk.FromDense(levels, channels, values);

        var hot = source.MarkHot();
        Assert.True(hot.IsHot);
        Assert.Equal(DenseVoxelLayout.Morton, hot.Layout);
        Assert.Equal(17u, hot[0, 1, 2, 3]);
        var morton = DenseVoxel.GetIndex(3, 1, 2, levels, DenseVoxelLayout.Morton);
        Assert.Equal(23u, hot.GetChannelSpan(1)[morton]);

        var dense = hot.AsSpan();
        dense[0, 1, 2, 3] = 41;
        dense[1, 0, 0, 0] = 99;
        var committed = hot.Commit();

        Assert.False(hot.IsHot);
        Assert.Same(committed, hot.CommittedChunk);
        Assert.Same(committed, hot.Commit());
        Assert.Equal(41u, committed.GetChannel(0).Get(1, 2, 3));
        Assert.Equal(99u, committed.GetChannel(1).Get(0, 0, 0));
        Assert.Equal(17u, source.GetChannel(0).Get(1, 2, 3));
        Assert.Throws<InvalidOperationException>(() => _ = hot[0, 0, 0, 0]);
    }

    [Fact]
    public void EmptyAndGenericHotChunksRoundTrip()
    {
        var empty = HotOctreeChunk.Empty(2, 2);
        empty[1, 3, 2, 1] = 7;
        var cold = empty.UnmarkHot();
        Assert.Equal(7u, cold.GetChannel(1).Get(3, 2, 1));

        var generic = HotOctreeChunk<ushort>.Empty(2, 1);
        generic[0, 2, 1, 3] = 1234;
        var genericCold = generic.Commit();
        Assert.Equal((ushort)1234, genericCold.GetChannel(0).Get(2, 1, 3));
        Assert.False(generic.IsHot);
    }

    [Fact]
    public void BoundedWorldPublishesHotEditsAtomicallyAndPreservesSourceSnapshot()
    {
        using var world = new OctreeWorld(4, 2, 1, 1, initiallyEmpty: true);
        var source = Chunk(2, 5);
        world.LoadChunk(1, 0, 0, source);
        var hot = world.MarkChunkHot(1, 0, 0);
        hot[0, 3, 2, 1] = 77;

        Assert.Equal(5u, world.Get(0, 7, 2, 1));
        var revision = world.Revision;
        OctreeChunk committed;
        using (world.BeginWriteBatch())
        {
            committed = world.CommitHotChunk(1, 0, 0, hot);
            Assert.Equal(77u, world.Get(0, 7, 2, 1));
        }

        Assert.Equal(revision + 1, world.Revision);
        Assert.False(hot.IsHot);
        Assert.Equal(5u, source.GetChannel(0).Get(3, 2, 1));
        Assert.True(world.TryGetChunk(1, 0, 0, out var resident));
        Assert.Same(committed, resident);
    }

    [Fact]
    public void BoundedWorldCanPromoteKnownEmptyButNotUnloadedChunks()
    {
        using var unloaded = new OctreeWorld(3, 1, 1, 1);
        Assert.False(unloaded.TryMarkChunkHot(0, 0, 0, out var absent));
        Assert.Null(absent);

        using var world = new OctreeWorld(3, 1, 1, 1, initiallyEmpty: true);
        var hot = world.MarkChunkHot(1, 1, 1);
        Assert.Equal(0u, hot[0, 1, 0, 0]);
        hot[0, 1, 0, 0] = 12;

        world.UnmarkChunkHot(1, 1, 1, hot);

        Assert.Equal(12u, world.Get(0, 3, 2, 2));
        Assert.Equal(1, world.ResidentChunkCount);
    }

    [Fact]
    public void StaleHotChunkCannotOverwriteANewerSnapshot()
    {
        using var world = new OctreeWorld(3, 1, 1, 1, initiallyEmpty: true);
        world.LoadChunk(0, 0, 0, Chunk(1, 3));
        var hot = world.MarkChunkHot(0, 0, 0);
        hot[0, 0, 0, 0] = 7;
        world.LoadChunk(0, 0, 0, Chunk(1, 9));

        Assert.False(world.TryCommitHotChunk(0, 0, 0, hot));
        Assert.True(hot.IsHot);
        Assert.Equal(7u, hot[0, 0, 0, 0]);
        Assert.Equal(9u, world.Get(0, 0, 0, 0));
    }

    [Fact]
    public void HotCommitUsesTheWorldWriteLock()
    {
        using var world = new OctreeWorld(3, 1, 1, 1, initiallyEmpty: true);
        world.LoadChunk(0, 0, 0, Chunk(1, 2));
        var hot = world.MarkChunkHot(0, 0, 0);
        hot[0, 0, 0, 0] = 8;
        using var started = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        Worker commit;

        using (world.BeginReadBatch())
        {
            commit = new Worker(() =>
            {
                started.Set();
                world.CommitHotChunk(0, 0, 0, hot);
                finished.Set();
            });
            Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(finished.Wait(150));
            Assert.Equal(2u, world.Get(0, 0, 0, 0));
        }

        commit.Join();
        Assert.True(finished.IsSet);
        Assert.Equal(8u, world.Get(0, 0, 0, 0));
    }

    [Fact]
    public void WorldEntityPublishesHotEditsAtSignedChunkCoordinates()
    {
        var world = new WorldEntity(2, 1);
        var coordinate = new ChunkCoordinate(-2, 3, -4);
        world.SetChunk(coordinate, Chunk(1, 6));
        var hot = world.MarkChunkHot(coordinate);
        hot[0, 1, 1, 0] = 42;

        var committed = world.CommitHotChunk(coordinate, hot);

        Assert.Equal(42u, world.Get(0, -3, 7, -8));
        Assert.True(world.TryGetChunk(coordinate, out var resident));
        Assert.Same(committed, resident);
        Assert.False(world.TryMarkChunkHot(99, 99, 99, out _));
    }

    [Fact]
    public void GenericWorldPublishesExactHotValues()
    {
        using var world = new OctreeWorld<ushort>(3, 1, 1, 1, initiallyEmpty: true);
        var hot = world.MarkChunkHot(1, 0, 0);
        hot[0, 1, 1, 1] = 54321;

        var committed = world.CommitHotChunk(1, 0, 0, hot);

        Assert.Equal((ushort)54321, world.Get(0, 3, 1, 1));
        Assert.Same(committed, hot.CommittedChunk);
    }

    private static OctreeChunk Chunk(int levels, uint value)
    {
        var count = 1 << (levels * 3);
        return OctreeChunk.FromDense(levels, 1, Enumerable.Repeat(value, count).ToArray());
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
            Assert.True(_thread.Join(TimeSpan.FromSeconds(10)),
                "Worker did not complete; possible deadlock.");
            if (_error is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_error).Throw();
        }
    }
}
