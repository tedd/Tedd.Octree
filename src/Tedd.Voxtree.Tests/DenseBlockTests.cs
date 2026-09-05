using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Tedd.Voxtree;
using Model = global::Tedd.Voxtree.Octree;

namespace Tedd.Voxtree.Tests;

public class DenseBlockTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void MortonBuildsMatchLinearEncodingAndUnalignedExtraction(int pattern)
    {
        var values = SpatialQueryTests.CreateValues(pattern, 16);
        var morton = new uint[values.Length];
        DenseVoxel.Convert(values, morton, 4, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
        var linear = new Model(4, values); var tree = new Model(4);
        tree.Build(morton, DenseVoxelLayout.Morton);
        Assert.Equal(linear.Data.ToArray(), tree.Data.ToArray());
        var bytes = new byte[Model.GetRequiredSize(morton, 4, DenseVoxelLayout.Morton)];
        Assert.True(Model.TryBuild(morton, 4, bytes, out var written, DenseVoxelLayout.Morton));
        Assert.Equal(bytes.Length, written); Assert.Equal(linear.Data.ToArray(), bytes);
        var output = new uint[513]; output[^1] = 999;
        foreach (var layout in new[] { DenseVoxelLayout.Linear, DenseVoxelLayout.Morton })
        {
            tree.CopyBlockTo(3, 5, 7, 3, output, layout);
            for (var x = 0; x < 8; x++)
            for (var y = 0; y < 8; y++)
            for (var z = 0; z < 8; z++)
                Assert.Equal(linear.Get(x + 3, y + 5, z + 7), output[DenseVoxel.GetIndex(x, y, z, 3, layout)]);
            Assert.Equal(999u, output[^1]);
        }
    }
    [Fact]
    public void MortonOrderingUsesPackageConventionAtBoundaryDepths()
    {
        Assert.Equal(1, DenseVoxel.GetIndex(1, 0, 0, 1, DenseVoxelLayout.Morton));
        Assert.Equal(4, DenseVoxel.GetIndex(0, 0, 1, 1, DenseVoxelLayout.Morton));
        Assert.Equal(134217727, DenseVoxel.GetIndex(511, 511, 511, 9, DenseVoxelLayout.Morton));
        var source = new uint[] { uint.MaxValue }; var result = new uint[1];
        var tree = new Model(0); tree.Build(source, DenseVoxelLayout.Morton);
        tree.CopyBlockTo(0, 0, 0, 0, result, DenseVoxelLayout.Morton); Assert.Equal(source, result);
        Assert.Throws<ArgumentOutOfRangeException>(() => DenseVoxel.GetIndex(512, 0, 0, 9, DenseVoxelLayout.Morton));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Build(source, (DenseVoxelLayout)9));
    }
    [Fact]
    public void MultiChannelDenseEditingPersistenceAndSharedSnapshotsRoundTrip()
    {
        var values = new uint[512 * 4];
        var dense = new DenseVoxelBlockSpan(values, 3, 4, DenseVoxelLayout.Morton);
        dense[0, 1, 2, 3] = 99; dense[1, 1, 2, 3] = 5; dense[2, 2, 4, 6] = uint.MaxValue;
        dense.GetChannelSpan(3).Fill(7);
        var chunk = OctreeChunk.FromDense(3, 4, values, DenseVoxelLayout.Morton);
        var output = new uint[values.Length]; chunk.CopyBlockTo(0, 0, 0, 3, output, DenseVoxelLayout.Morton);
        Assert.Equal(values, output);
        var packet = new byte[chunk.SerializedLength + 1]; packet[^1] = 123;
        var written = chunk.CopyEncodedTo(packet); Assert.Equal(123, packet[^1]);
        var loaded = OctreeChunk.FromEncoded(packet.AsMemory(0, written));
        loaded.CopyBlockTo(0, 0, 0, 3, output, DenseVoxelLayout.Morton); Assert.Equal(values, output);
        Assert.True(loaded.GetChannelData(0).Span.Overlaps(packet));
        dense.GetChannelSpan(1).Fill(12);
        var changed = chunk.WithDenseChannel(1, dense.GetChannelSpan(1), DenseVoxelLayout.Morton);
        Assert.Equal(chunk.GetChannelData(0), changed.GetChannelData(0));
        Assert.Equal(5u, chunk.GetChannel(1).Get(1, 2, 3)); Assert.Equal(12u, changed.GetChannel(1).Get(1, 2, 3));
        var cache = new OctreeNeighborhoodCache(4);
        cache.Update(chunk.GetChannelData(1), 1, 2, 2, 2);
        Assert.Equal(5u, cache.Get(1, 2, 3));
        cache.Update(changed.GetChannelData(1), 2, 2, 2, 2);
        Assert.Equal(12u, cache.Get(1, 2, 3));
    }
    [Fact]
    public void ChunkPacketRejectsTruncationTrailingBytesAndCorruptedChannels()
    {
        var chunk = OctreeChunk.Empty(3, 4); var data = new byte[chunk.SerializedLength]; chunk.CopyEncodedTo(data);
        for (var length = 0; length < data.Length; length++)
            Assert.Throws<FormatException>(() => OctreeChunk.FromEncoded(data.AsMemory(0, length)));
        var corrupt = (byte[])data.Clone(); corrupt[16] = 0;
        Assert.Throws<FormatException>(() => OctreeChunk.FromEncoded(corrupt));
        corrupt = (byte[])data.Clone(); BinaryPrimitives.WriteInt32LittleEndian(corrupt.AsSpan(4), int.MaxValue);
        Assert.Throws<FormatException>(() => OctreeChunk.FromEncoded(corrupt));
        Assert.Throws<FormatException>(() => OctreeChunk.FromEncoded(data.Concat(new byte[] { 0 }).ToArray()));
    }
    [Fact]
    public void BlocksRejectAliasingAndInvalidCapacityBeforeWriting()
    {
        var values = new uint[512];
        Assert.Throws<ArgumentException>(() => DenseVoxel.Convert(values, values, 3, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton));
        DenseVoxel.Convert(values, values, 3, DenseVoxelLayout.Linear, DenseVoxelLayout.Linear);
        var chunk = OctreeChunk.FromDense(3, 1, SpatialQueryTests.CreateValues(2, 8));
        var bytes = chunk.GetChannelData(0).ToArray();
        var borrowed = new OctreeChunk(3, new ReadOnlyMemory<byte>[] { bytes });
        var copy = (byte[])bytes.Clone();
        Assert.Throws<ArgumentException>(() => ExtractAliased(borrowed, bytes)); Assert.Equal(copy, bytes);
        Assert.Throws<ArgumentException>(() => chunk.CopyBlockTo(0, 0, 0, 3, new uint[511]));
        Assert.Throws<ArgumentOutOfRangeException>(() => chunk.CopyBlockTo(1, 0, 0, 3, new uint[512]));
    }
    private static void ExtractAliased(OctreeChunk chunk, byte[] bytes) =>
        chunk.CopyBlockTo(0, 0, 0, 1, MemoryMarshal.Cast<byte, uint>(bytes.AsSpan(0, 32)), DenseVoxelLayout.Morton);
    [Fact]
    public void DenseImportsExportsAndConversionAllocateZeroAfterProvisioning()
    {
        var linear = SpatialQueryTests.CreateValues(3, 16); var morton = new uint[linear.Length];
        var destination = new byte[Model.GetMaximumSize(4)]; var output = new uint[512];
        for (var i = 0; i < 20; i++) Exercise(linear, morton, destination, output);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) Exercise(linear, morton, destination, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    private static void Exercise(uint[] linear, uint[] morton, byte[] destination, uint[] output)
    {
        DenseVoxel.Convert(linear, morton, 4, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
        var written = Model.Build(morton, 4, destination, DenseVoxelLayout.Morton);
        new OctreeSpan(destination.AsSpan(0, written)).CopyBlockTo(1, 2, 3, 3, output, DenseVoxelLayout.Morton);
    }
}
