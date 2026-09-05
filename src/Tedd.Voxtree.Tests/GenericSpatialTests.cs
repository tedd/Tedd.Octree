using System.Runtime.InteropServices;

namespace Tedd.Voxtree.Tests;

public class GenericSpatialTests
{
    [Fact]
    public void GenericQueries_Preserve128BitStructValues()
    {
        var values = new Value128[64];
        values[TestData.Index(4, 1, 2, 3)] = new Value128(0x11, 0x22);
        values[TestData.Index(4, 3, 0, 1)] = new Value128(0x33, 0x44);
        var tree = new Octree<Value128>(2, values);
        var bounds = new VoxelBox(0, 0, 0, 4, 4, 4);

        Assert.True(tree.Any(bounds, VoxelFilter<Value128>.EqualTo(new Value128(0x11, 0x22))));
        Assert.Equal(2, tree.CountMatches(bounds, VoxelFilter<Value128>.NonZero));

        Span<VoxelHit<Value128>> hits = stackalloc VoxelHit<Value128>[2];
        Assert.True(tree.Query(bounds, VoxelFilter<Value128>.NonZero, hits, out var written));
        Assert.Equal(2, written);
        Assert.Contains(hits.ToArray(), hit => hit.X == 1 && hit.Y == 2 && hit.Z == 3 &&
                                               hit.Value == new Value128(0x11, 0x22));

        Assert.True(tree.TryFindNearest(0, 2, 3, 4, VoxelFilter<Value128>.NonZero, out var nearest));
        Assert.Equal((1, 2, 3), (nearest.X, nearest.Y, nearest.Z));
    }

    [Fact]
    public void GenericMaskedFilter_AppliesToRawStructBits()
    {
        var value = new Value128(0xf0, 0xaa00);
        var mask = new Value128(0x0f, 0xff00);
        var expected = new Value128(0, 0xaa00);

        Assert.True(VoxelFilter<Value128>.Masked(mask, expected).Matches(value));
        Assert.False(VoxelFilter<Value128>.EqualTo(expected).Matches(value));
    }

    [Fact]
    public void GenericMortonBlocksAndDenseView_RoundTrip()
    {
        var linear = Enumerable.Range(0, 64).Select(i => (long)(i - 32)).ToArray();
        var morton = new long[64];
        DenseVoxel<long>.Convert(linear, morton, 2, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
        var tree = new Octree<long>(2);
        tree.Build(morton, DenseVoxelLayout.Morton);
        var output = new long[64];
        tree.CopyBlockTo(0, 0, 0, 2, output);
        Assert.Equal(linear, output);

        var channels = new long[128];
        var view = new DenseVoxelBlockSpan<long>(channels, 2, 2);
        view[1, 3, 2, 1] = -123;
        Assert.Equal(-123, view.GetChannelSpan(1)[TestData.Index(4, 3, 2, 1)]);
    }

    [Fact]
    public void GenericChunk_PersistsAndRehydratesAllChannels()
    {
        var values = Enumerable.Range(0, 128).Select(i => (ushort)(i * 17)).ToArray();
        var chunk = OctreeChunk<ushort>.FromDense(2, 2, values);
        var packet = new byte[chunk.SerializedLength];
        Assert.Equal(packet.Length, chunk.CopyEncodedTo(packet));

        var restored = OctreeChunk<ushort>.FromEncoded(packet);
        var output = new ushort[128];
        restored.CopyBlockTo(0, 0, 0, 2, output);
        Assert.Equal(values, output);
        Assert.Throws<FormatException>(() => OctreeChunk<ulong>.FromEncoded(packet));
    }

    [Fact]
    public void GenericNeighborhoodCache_ReusesItsTypedStorage()
    {
        var values = Enumerable.Range(0, 512).Select(i => (ulong)i).ToArray();
        var tree = new Octree<ulong>(3, values);
        var cache = new OctreeNeighborhoodCache<ulong>(4);

        Assert.Equal(64, cache.Update(tree, 4, 4, 4));
        Assert.Equal(tree.Get(4, 4, 4), cache.Get(4, 4, 4));
        Assert.Equal(0, cache.Update(tree, 4, 4, 4));
        Assert.True(cache.Any(cache.Bounds, VoxelFilter<ulong>.NonZero));
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Value128(ulong Low, ulong High);
}
