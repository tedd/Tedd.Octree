using System.Runtime.InteropServices;

namespace Tedd.Voxtree.Tests;

public class GenericWorldTests
{
    [Fact]
    public void GenericWorld_LoadQueryExtractAndEvict_PreserveStructValues()
    {
        var dense = new WorldValue[64];
        dense[TestData.Index(4, 1, 2, 3)] = new WorldValue(7, 11);
        var chunk = OctreeChunk<WorldValue>.FromDense(2, 1, dense);
        using var world = new OctreeWorld<WorldValue>(3, 2, 1, 2);

        Assert.True(world.TryLoadChunk(1, 0, 1, chunk));
        Assert.Equal(new WorldValue(7, 11), world.Get(0, 5, 2, 7));
        Assert.True(world.TryAny(new VoxelBox(4, 0, 4, 8, 4, 8), 0,
            VoxelFilter<WorldValue>.NonZero, out var any));
        Assert.True(any);
        Assert.True(world.TryCountMatches(new VoxelBox(4, 0, 4, 8, 4, 8), 0,
            VoxelFilter<WorldValue>.NonZero, out var count));
        Assert.Equal(1, count);

        var output = new WorldValue[64];
        world.CopyChannelBlockTo(0, 4, 0, 4, 2, output);
        Assert.Equal(dense, output);

        Assert.True(world.TryUnloadRegion(4, 0, 4, 2));
        Assert.False(world.TryGet(0, 5, 2, 7, out _));
    }

    [Fact]
    public void GenericWorld_KnownEmptyUsesTheZeroBitPattern()
    {
        using var world = new OctreeWorld<ulong>(4, 2, 1, 1, initiallyEmpty: true);
        var box = new VoxelBox(0, 0, 0, 16, 16, 16);

        Assert.True(world.IsKnownEmpty(box));
        Assert.Equal(0ul, world.Get(0, 7, 8, 9));
        Assert.True(world.TryCountMatches(box, 0, VoxelFilter<ulong>.EqualTo(0), out var count));
        Assert.Equal(4096, count);
    }

    [Fact]
    public void GenericWorld_BatchesReuseLocks()
    {
        using var world = new OctreeWorld<UInt128>(2, 1, 1, 1, initiallyEmpty: true);
        using (world.BeginWriteBatch())
        {
            Assert.True(world.TryUnloadRegion(0, 0, 0, 1));
            Assert.True(world.TrySetEmptyRegion(0, 0, 0, 1));
        }
        using (world.BeginReadBatch())
        {
            Assert.Equal(UInt128.Zero, world.Get(0, 0, 0, 0));
            Assert.Equal(2ul, world.Revision);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct WorldValue(ulong Low, ulong High);
}
