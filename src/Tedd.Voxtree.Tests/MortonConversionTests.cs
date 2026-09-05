using System.Runtime.InteropServices;
using Tedd.Voxtree;
using Model = global::Tedd.Voxtree.Octree;

namespace Tedd.Voxtree.Tests;

public class MortonConversionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void LayoutConversionMatchesPackageAndPreservesDestinationEdges(int levels)
    {
        var linear = TestData.Coordinates(levels);
        var expected = ToMorton(linear, levels);
        var storage = new uint[linear.Length + 9];
        Array.Fill(storage, uint.MaxValue);
        DenseVoxel.Convert(linear, storage.AsSpan(3), levels, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
        Assert.True(expected.AsSpan().SequenceEqual(storage.AsSpan(3, linear.Length)));
        Assert.All(storage[..3], value => Assert.Equal(uint.MaxValue, value));
        Assert.All(storage[(3 + linear.Length)..], value => Assert.Equal(uint.MaxValue, value));
        DenseVoxel.Convert(expected, storage.AsSpan(3), levels, DenseVoxelLayout.Morton, DenseVoxelLayout.Linear);
        Assert.True(linear.AsSpan().SequenceEqual(storage.AsSpan(3, linear.Length)));
        Assert.All(storage[..3], value => Assert.Equal(uint.MaxValue, value));
        Assert.All(storage[(3 + linear.Length)..], value => Assert.Equal(uint.MaxValue, value));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    [InlineData(63)] [InlineData(64)] [InlineData(65)] [InlineData(511)]
    [InlineData(512)] [InlineData(513)] [InlineData(4095)]
    public void UnalignedRunsPreserveCanonicalEncodingAndRoundTrip(int runLength)
    {
        var morton = new uint[4096];
        uint[] palette = [0, 127, 128, 16383, 16384, uint.MaxValue];
        for (var i = 0; i < morton.Length; i++) morton[i] = palette[(i / runLength) % palette.Length];
        AssertRoundTrip(morton, 4);
    }

    [Fact]
    public void SingleVoxelChangesAtEveryPositionPreserveOctantOrdering()
    {
        var morton = new uint[512];
        Array.Fill(morton, 7u);
        for (var position = 0; position < morton.Length; position++)
        {
            morton[position] = uint.MaxValue;
            AssertRoundTrip(morton, 3);
            morton[position] = 7;
        }
    }

    [Theory]
    [InlineData(0, 0u)] [InlineData(0, uint.MaxValue)]
    [InlineData(1, 128u)] [InlineData(5, uint.MaxValue)]
    public void UniformAndSingleVoxelVolumesRoundTrip(int levels, uint value) =>
        AssertRoundTrip(TestData.Constant(levels, value), levels);

    [Fact]
    public void DenseFallbackPreservesLinearLittleEndianPayload()
    {
        var linear = TestData.XorShift(4);
        var morton = ToMorton(linear, 4);
        var tree = new Model(4);
        tree.Build(morton, DenseVoxelLayout.Morton);
        Assert.Equal(2, tree.Data.Span[1] & 3);
        AssertRoundTrip(morton, 4);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void AlignedAndUnalignedSubblocksMatchPointQueries(int pattern)
    {
        var tree = new Model(4, SpatialQueryTests.CreateValues(pattern, 16));
        foreach (var origin in new[] { (0, 0, 0), (8, 0, 8), (0, 8, 0), (3, 5, 7) })
        {
            var output = new uint[513];
            output[^1] = uint.MaxValue;
            tree.CopyBlockTo(origin.Item1, origin.Item2, origin.Item3, 3, output, DenseVoxelLayout.Morton);
            for (var x = 0; x < 8; x++)
            for (var y = 0; y < 8; y++)
            for (var z = 0; z < 8; z++)
                Assert.Equal(tree.Get(x + origin.Item1, y + origin.Item2, z + origin.Item3),
                    output[(int)global::Tedd.MortonEncoding.Encode((uint)x, (uint)y, (uint)z)]);
            Assert.Equal(uint.MaxValue, output[^1]);
        }
    }

    [Fact]
    public void MortonDecodeRejectsMalformedStructureAndInvalidDestinationsBeforeWriting()
    {
        var tree = new Model(3, TestData.SparseOctants(3));
        var encoded = tree.Data.ToArray();
        encoded[encoded.Length - 1 - encoded[^1]] ^= 1; // Turn a uniform value into an invalid child pointer.
        var output = TestData.Constant(3, uint.MaxValue);
        Assert.False(new OctreeSpan(encoded).TryCopyTo(output, DenseVoxelLayout.Morton));
        Assert.All(output, value => Assert.Equal(uint.MaxValue, value));
        Assert.Throws<FormatException>(() => new OctreeSpan(encoded).CopyTo(output, DenseVoxelLayout.Morton));
        Assert.False(tree.TryCopyTo(output.AsSpan(1), DenseVoxelLayout.Morton));
        Assert.Throws<ArgumentException>(() => tree.CopyTo(output.AsSpan(1), DenseVoxelLayout.Morton));
        Assert.All(output, value => Assert.Equal(uint.MaxValue, value));

        var storage = new byte[output.Length * sizeof(uint)];
        tree.Data.Span.CopyTo(storage);
        var snapshot = (byte[])storage.Clone();
        var view = new OctreeSpan(storage.AsSpan(0, tree.EncodedLength));
        Assert.False(view.TryCopyTo(MemoryMarshal.Cast<byte, uint>(storage), DenseVoxelLayout.Morton));
        Assert.Equal(snapshot, storage);
        Assert.Throws<ArgumentException>(() => new OctreeSpan(storage.AsSpan(0, tree.EncodedLength))
            .CopyTo(MemoryMarshal.Cast<byte, uint>(storage), DenseVoxelLayout.Morton));
        Assert.Equal(snapshot, storage);
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.TryCopyTo(output, (DenseVoxelLayout)99));
        Assert.False(new Model(3).TryCopyTo(output, DenseVoxelLayout.Morton));
        Assert.False(default(OctreeSpan).TryCopyTo(output, DenseVoxelLayout.Morton));
    }

    [Fact]
    public void MortonCallerBufferBuildAndFullDecodeAllocateNothing()
    {
        var source = TestData.SparseOctants(4);
        var encoded = new byte[Model.GetMaximumSize(4)];
        var output = new uint[source.Length];
        for (var i = 0; i < 30; i++) Exercise(source, encoded, output);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) Exercise(source, encoded, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void Exercise(uint[] source, byte[] encoded, uint[] output)
    {
        var written = Model.Build(source, 4, encoded, DenseVoxelLayout.Morton);
        new OctreeSpan(encoded.AsSpan(0, written)).CopyTo(output, DenseVoxelLayout.Morton);
        new OctreeSpan(encoded.AsSpan(0, written)).CopyBlockTo(0, 0, 0, 4, output, DenseVoxelLayout.Morton);
    }

    private static void AssertRoundTrip(uint[] morton, int levels)
    {
        var side = 1 << levels;
        var linear = new uint[morton.Length];
        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
            linear[(x * side + y) * side + z] = morton[(int)global::Tedd.MortonEncoding.Encode((uint)x, (uint)y, (uint)z)];
        var canonical = new Model(levels, linear);
        var tree = new Model(levels);
        tree.Build(morton, DenseVoxelLayout.Morton);
        Assert.True(canonical.Data.Span.SequenceEqual(tree.Data.Span));
        var length = Model.GetRequiredSize(morton, levels, DenseVoxelLayout.Morton);
        Assert.Equal(canonical.EncodedLength, length);
        var encoded = new byte[length + 7];
        Array.Fill(encoded, (byte)0xa5);
        Assert.True(Model.TryBuild(morton, levels, encoded.AsSpan(3, length), out var written, DenseVoxelLayout.Morton));
        Assert.Equal(length, written);
        Assert.True(canonical.Data.Span.SequenceEqual(encoded.AsSpan(3, written)));
        Assert.All(encoded[..3], value => Assert.Equal(0xa5, value));
        Assert.All(encoded[(3 + written)..], value => Assert.Equal(0xa5, value));
        Assert.False(Model.TryBuild(morton, levels, encoded.AsSpan(0, length - 1), out written, DenseVoxelLayout.Morton));
        Assert.Equal(0, written);
        var output = new uint[morton.Length + 1];
        output[^1] = uint.MaxValue;
        var restored = Model.FromEncoded(tree.Data.Span);
        Assert.True(restored.TryCopyTo(output, DenseVoxelLayout.Morton));
        Assert.True(morton.AsSpan().SequenceEqual(output.AsSpan(0, morton.Length)));
        restored.AsSpan().CopyTo(output, DenseVoxelLayout.Morton);
        Assert.True(morton.AsSpan().SequenceEqual(output.AsSpan(0, morton.Length)));
        restored.CopyBlockTo(0, 0, 0, levels, output, DenseVoxelLayout.Morton);
        Assert.True(morton.AsSpan().SequenceEqual(output.AsSpan(0, morton.Length)));
        Assert.Equal(uint.MaxValue, output[^1]);
        restored.CopyTo(output);
        Assert.True(linear.AsSpan().SequenceEqual(output.AsSpan(0, linear.Length)));
    }

    private static uint[] ToMorton(uint[] linear, int levels)
    {
        var side = 1 << levels;
        var morton = new uint[linear.Length];
        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
            morton[(int)global::Tedd.MortonEncoding.Encode((uint)x, (uint)y, (uint)z)] = linear[(x * side + y) * side + z];
        return morton;
    }
}
