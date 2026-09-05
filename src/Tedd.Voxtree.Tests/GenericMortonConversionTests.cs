using System.Runtime.InteropServices;

namespace Tedd.Voxtree.Tests;

public class GenericMortonConversionTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(8)] [InlineData(16)]
    public void AllStorageWidthsPreserveBitsAndCanonicalMortonRoundTrips(int width)
    {
        switch (width)
        {
            case 1: CheckPatterns<byte>(); break;
            case 2: CheckPatterns<ushort>(); break;
            case 4: CheckPatterns<uint>(); break;
            case 8: CheckPatterns<ulong>(); break;
            case 16: CheckPatterns<Cell128>(); break;
        }
    }

    private static void CheckPatterns<T>() where T : unmanaged
    {
        foreach (var levels in new[] { 0, 1, 3 })
        foreach (var pattern in new[] { 0, 1, 2, 3, 4 })
        {
            var source = new T[1 << (levels * 3)];
            var palette = new T[3];
            MemoryMarshal.AsBytes(palette.AsSpan(1, 1)).Fill(0x80);
            MemoryMarshal.AsBytes(palette.AsSpan(2, 1)).Fill(0xff);
            for (var i = 0; i < source.Length; i++)
                source[i] = pattern switch
                {
                    0 => palette[2],
                    1 => i == source.Length - 1 ? palette[2] : palette[0],
                    2 => palette[(i / 7) % 3],
                    3 => palette[(i / 65) % 3],
                    _ => default
                };
            if (pattern == 4) new Random(42).NextBytes(MemoryMarshal.AsBytes(source.AsSpan()));
            CheckRoundTrip(source, levels);
        }
    }

    private static void CheckRoundTrip<T>(T[] source, int levels) where T : unmanaged
    {
        var side = 1 << levels;
        var linear = new T[source.Length];
        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
            linear[(x * side + y) * side + z] = source[(int)global::Tedd.MortonEncoding.Encode((uint)x, (uint)y, (uint)z)];
        var output = new T[source.Length + 3];
        var sentinelBytes = MemoryMarshal.AsBytes(output.AsSpan(source.Length));
        sentinelBytes.Fill(0xa5);
        DenseVoxel<T>.Convert(source, output, levels, DenseVoxelLayout.Morton, DenseVoxelLayout.Linear);
        Assert.True(MemoryMarshal.AsBytes(linear.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(output.AsSpan(0, source.Length))));
        DenseVoxel<T>.Convert(linear, output, levels, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
        Assert.True(MemoryMarshal.AsBytes(source.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(output.AsSpan(0, source.Length))));
        var canonical = new Octree<T>(levels, linear);
        var tree = new Octree<T>(levels);
        tree.Build(source, DenseVoxelLayout.Morton);
        Assert.True(canonical.Data.Span.SequenceEqual(tree.Data.Span));
        var length = Octree<T>.GetRequiredSize(source, levels, DenseVoxelLayout.Morton);
        Assert.Equal(tree.EncodedLength, length);
        var encoded = new byte[length];
        Assert.True(Octree<T>.TryBuild(source, levels, encoded, out var written, DenseVoxelLayout.Morton));
        Assert.Equal(length, written);
        Assert.True(tree.Data.Span.SequenceEqual(encoded));
        Assert.False(Octree<T>.TryBuild(source, levels, encoded.AsSpan(1), out written, DenseVoxelLayout.Morton));
        Assert.Equal(0, written);
        var restored = Octree<T>.FromEncoded(tree.Data.Span);
        restored.CopyTo(output, DenseVoxelLayout.Morton);
        CheckOutput();
        Assert.True(restored.AsSpan().TryCopyTo(output, DenseVoxelLayout.Morton));
        CheckOutput();
        restored.CopyBlockTo(0, 0, 0, levels, output, DenseVoxelLayout.Morton);
        CheckOutput();
        Assert.True(sentinelBytes.IndexOfAnyExcept((byte)0xa5) < 0);
        Assert.False(restored.TryCopyTo(output.AsSpan(0, source.Length - 1), DenseVoxelLayout.Morton));
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.CopyTo(output, (DenseVoxelLayout)9));
        if (levels == 3)
        {
            foreach (var origin in new[] { (4, 0, 4), (3, 2, 1) })
            {
                restored.CopyBlockTo(origin.Item1, origin.Item2, origin.Item3, 2, output, DenseVoxelLayout.Morton);
                for (var x = 0; x < 4; x++)
                for (var y = 0; y < 4; y++)
                for (var z = 0; z < 4; z++)
                    Assert.Equal(restored.Get(x + origin.Item1, y + origin.Item2, z + origin.Item3),
                        output[(int)global::Tedd.MortonEncoding.Encode((uint)x, (uint)y, (uint)z)]);
            }
        }
        void CheckOutput() => Assert.True(MemoryMarshal.AsBytes(source.AsSpan())
            .SequenceEqual(MemoryMarshal.AsBytes(output.AsSpan(0, source.Length))));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GenericMortonConversionAndDecodeAllocateNothing(bool uniform)
    {
        var source = new Cell128[512];
        MemoryMarshal.AsBytes(source.AsSpan()).Fill(0x80);
        if (!uniform) source[^1] = default;
        var encoded = new byte[Octree<Cell128>.GetMaximumSize(3)];
        var morton = new Cell128[512];
        var output = new Cell128[512];
        for (var i = 0; i < 30; i++) Exercise();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) Exercise();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        void Exercise()
        {
            DenseVoxel<Cell128>.Convert(source, morton, 3, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
            var length = Octree<Cell128>.Build(morton, 3, encoded, DenseVoxelLayout.Morton);
            var view = new OctreeSpan<Cell128>(encoded.AsSpan(0, length));
            view.CopyTo(output, DenseVoxelLayout.Morton);
            view.CopyBlockTo(0, 0, 0, 3, output, DenseVoxelLayout.Morton);
        }
    }

    [Theory]
    [InlineData(4)] [InlineData(5)]
    public void MortonWorldCopiesPreserveChannelAndChunkOffsets(int origin)
    {
        using var world = new OctreeWorld<ulong>(4, 2, 2, 8, initiallyEmpty: true);
        for (var cx = 1; cx <= 2; cx++)
        for (var cy = 1; cy <= 2; cy++)
        for (var cz = 1; cz <= 2; cz++)
        {
            var values = new ulong[128];
            Array.Fill(values, 7ul, 0, 64);
            values[63] = (ulong)(cx * 100 + cy * 10 + cz);
            for (var i = 64; i < 128; i++) values[i] = ulong.MaxValue - (ulong)(i * 1000 + cx * 100 + cy * 10 + cz);
            world.LoadChunk(cx, cy, cz, OctreeChunk<ulong>.FromDense(2, 2, values));
        }
        var output = new ulong[1025];
        output[^1] = ulong.MaxValue;
        world.CopyBlockTo(origin, origin, origin, 3, output, DenseVoxelLayout.Morton);
        for (var channel = 0; channel < 2; channel++)
        for (var x = 0; x < 8; x++)
        for (var y = 0; y < 8; y++)
        for (var z = 0; z < 8; z++)
            Assert.Equal(world.Get(channel, origin + x, origin + y, origin + z),
                output[channel * 512 + (int)global::Tedd.MortonEncoding.Encode((uint)x, (uint)y, (uint)z)]);
        Assert.Equal(ulong.MaxValue, output[^1]);
    }

    [Fact]
    public void GenericMortonDecodeRejectsCorruptionAndAliasingBeforeWriting()
    {
        var values = new ulong[512];
        values[^1] = ulong.MaxValue;
        var tree = new Octree<ulong>(3, values);
        var corrupt = tree.Data.ToArray();
        corrupt[corrupt.Length - 1 - corrupt[^1]] ^= 1;
        var output = new ulong[512];
        Array.Fill(output, ulong.MaxValue);
        Assert.False(new OctreeSpan<ulong>(corrupt).TryCopyTo(output, DenseVoxelLayout.Morton));
        Assert.All(output, v => Assert.Equal(ulong.MaxValue, v));
        var storage = new byte[512 * sizeof(ulong)];
        tree.Data.Span.CopyTo(storage);
        var snapshot = (byte[])storage.Clone();
        Assert.False(new OctreeSpan<ulong>(storage.AsSpan(0, tree.EncodedLength))
            .TryCopyTo(MemoryMarshal.Cast<byte, ulong>(storage), DenseVoxelLayout.Morton));
        Assert.Equal(snapshot, storage);
    }

    // Opaque storage exercises raw-bit preservation without relying on user-defined field equality.
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    private struct Cell128 { }
}
