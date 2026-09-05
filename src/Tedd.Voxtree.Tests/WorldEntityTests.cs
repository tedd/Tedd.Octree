namespace Tedd.Voxtree.Tests;

public sealed class WorldEntityTests
{
    [Fact]
    public void ChunkSizeMustBeASupportedPowerOfTwo()
    {
        foreach (var size in new[] { 1, 2, 4, 32, 512 })
        {
            var world = new WorldEntity(size, 1);
            Assert.Equal(size, world.ChunkSize);
            Assert.Equal((int)Math.Log2(size), world.ChunkShift);
        }

        foreach (var size in new[] { int.MinValue, -1, 0, 3, 31, 513, 1024 })
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEntity(size, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEntity(32, 0));
    }

    [Theory]
    [InlineData(-33, -2, 31)]
    [InlineData(-32, -1, 0)]
    [InlineData(-1, -1, 31)]
    [InlineData(0, 0, 0)]
    [InlineData(31, 0, 31)]
    [InlineData(32, 1, 0)]
    [InlineData(63, 1, 31)]
    public void GlobalCoordinatesUseArithmeticShiftAndMask(long voxelX, long chunkX, int localX)
    {
        var world = new WorldEntity(32, 1);
        world.ResolveCoordinates(voxelX, voxelX, voxelX, out var chunk,
            out var actualX, out var actualY, out var actualZ);
        Assert.Equal(new ChunkCoordinate(chunkX, chunkX, chunkX), chunk);
        Assert.Equal(localX, actualX);
        Assert.Equal(localX, actualY);
        Assert.Equal(localX, actualZ);
    }

    [Fact]
    public void GlobalReadsRouteAcrossPositiveAndNegativeChunks()
    {
        var world = new WorldEntity(2, 1);
        world.SetChunk(-1, 0, 0, Chunk(11));
        world.SetChunk(0, 0, 0, Chunk(22));
        world.SetChunk(new ChunkCoordinate(1, 0, 0), Chunk(33));

        Assert.Equal(11u, world.Get(0, -2, 0, 0));
        Assert.Equal(11u, world.Get(0, -1, 1, 1));
        Assert.Equal(22u, world.Get(0, 0, 0, 0));
        Assert.Equal(22u, world.Get(0, 1, 1, 1));
        Assert.Equal(33u, world.Get(0, 2, 0, 0));
        Assert.False(world.TryGet(0, -3, 0, 0, out _));
        Assert.False(world.TryGet(0, 4, 0, 0, out _));
        Assert.Throws<InvalidOperationException>(() => world.Get(0, 4, 0, 0));
    }

    [Fact]
    public void DictionaryAndRecentCacheStayCoherentAcrossReplacementRemovalAndClear()
    {
        var world = new WorldEntity(2, 1);
        for (var index = 0; index < 12; index++) world.SetChunk(index, 0, 0, Chunk((uint)index + 1));

        Assert.Equal(12, world.ChunkCount);
        Assert.Equal(12, world.Chunks.Count);
        Assert.Equal(10, WorldEntity.RecentChunkCapacity);
        for (var index = 0; index < 12; index++)
        {
            Assert.True(world.TryGetChunk(index, 0, 0, out var chunk));
            Assert.Equal((uint)index + 1, chunk!.GetChannel(0).Get(0, 0, 0));
        }

        world.SetChunk(11, 0, 0, Chunk(99));
        Assert.Equal(99u, world.Get(0, 22, 0, 0));
        Assert.True(world.RemoveChunk(11, 0, 0));
        Assert.False(world.TryGetChunk(11, 0, 0, out _));
        Assert.False(world.RemoveChunk(11, 0, 0));
        Assert.Equal(11, world.Chunks.Count);

        world.Clear();
        Assert.Empty(world.Chunks);
        Assert.False(world.TryGetChunk(0, 0, 0, out _));
    }

    [Fact]
    public void ChunksMustMatchTheConfiguredLocalSchema()
    {
        var world = new WorldEntity(4, 2);
        Assert.Throws<ArgumentException>(() => world.SetChunk(0, 0, 0, OctreeChunk.Empty(1, 2)));
        Assert.Throws<ArgumentException>(() => world.SetChunk(0, 0, 0, OctreeChunk.Empty(2, 1)));
        Assert.Throws<ArgumentNullException>(() => world.SetChunk(0, 0, 0, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.TryGet(2, 0, 0, 0, out _));
    }

    [Fact]
    public void GenericWorldEntityPreservesExactValues()
    {
        var world = new WorldEntity<ushort>(2, 1);
        var values = Enumerable.Repeat((ushort)1234, 8).ToArray();
        var chunk = OctreeChunk<ushort>.FromDense(1, 1, values);
        var coordinate = new ChunkCoordinate(long.MinValue >> 1, long.MaxValue >> 1, -7);
        world.SetChunk(coordinate, chunk);

        Assert.True(world.TryGetChunk(coordinate, out var retained));
        Assert.Same(chunk, retained);
        Assert.Equal((ushort)1234, world.Get(0, long.MinValue, long.MaxValue, -13));
        Assert.Single(world.Chunks);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEntity<ushort>(6, 1));
    }

    private static OctreeChunk Chunk(uint value) =>
        OctreeChunk.FromDense(1, 1, Enumerable.Repeat(value, 8).ToArray());
}
