using System.Runtime.InteropServices;

namespace Tedd.Voxtree.Tests;

public class GenericOctreeTests
{
    [Fact]
    public void FixedWidthBinaryIntegers_RoundTripWithoutNumericConversion()
    {
        AssertRoundTrip(new sbyte[] { -1, 0, 1, sbyte.MinValue, sbyte.MaxValue, 7, -7, 42 });
        AssertRoundTrip(new ushort[] { 0, 1, ushort.MaxValue, 42, 7, 9, 11, 13 });
        AssertRoundTrip(new int[] { int.MinValue, -1, 0, 1, int.MaxValue, 7, -7, 42 });
        AssertRoundTrip(new ulong[] { 0, 1, ulong.MaxValue, 42, 7, 9, 11, 13 });
        AssertRoundTrip(new Int128[] { Int128.MinValue, -1, 0, 1, Int128.MaxValue, 7, -7, 42 });
        AssertRoundTrip(new UInt128[] { 0, 1, UInt128.MaxValue, 42, 7, 9, 11, 13 });
    }

    [Fact]
    public void ExactWidthCustomStructs_RoundTripAtEverySupportedSize()
    {
        AssertRoundTrip(CreateValues(i => new Struct8((byte)i)));
        AssertRoundTrip(CreateValues(i => new Struct16((ushort)(i * 257))));
        AssertRoundTrip(CreateValues(i => new Struct32((uint)i * 0x0101_0101u)));
        AssertRoundTrip(CreateValues(i => new Struct64((ulong)i * 0x0101_0101_0101_0101ul)));
        AssertRoundTrip(CreateValues(i => new Struct128((ulong)i, ulong.MaxValue - (ulong)i)));
    }

    [Fact]
    public void NonPrimaryStructWidths_AreRejectedAtInitialization()
    {
        Assert.Throws<NotSupportedException>(() => new Octree<Struct24>(0));
        Assert.Throws<NotSupportedException>(() => new Octree<Struct96>(0));
        Assert.Throws<NotSupportedException>(() => new Octree<Struct192>(0));
    }

    [Fact]
    public void HeaderRejectsAReaderWithAnotherValueWidth()
    {
        var encoded = new Octree<uint>(1, new uint[] { 0, 1, 2, 3, 4, 5, 6, 7 }).Data.ToArray();

        Assert.False(OctreeSpan<ulong>.TryCreate(encoded, out _));
        Assert.Throws<FormatException>(() => new OctreeSpan<ulong>(encoded));
        Assert.Throws<FormatException>(() => Octree<ulong>.FromEncoded(encoded));
    }

    [Fact]
    public void UniformTreeAndDenseRepresentations_RoundTripCustom128BitValues()
    {
        var uniform = new Struct128[64];
        Array.Fill(uniform, new Struct128(ulong.MaxValue, 0x0123_4567_89ab_cdef));
        AssertRoundTrip(uniform);

        var sparse = new Struct128[64];
        sparse[^1] = new Struct128(1, ulong.MaxValue);
        AssertRoundTrip(sparse);

        var dense = CreateValues(i => new Struct128((ulong)i, ~(ulong)i));
        AssertRoundTrip(dense);
    }

    [Fact]
    public void PointReadsAndViewCreation_AllocateNothingAfterWarmup()
    {
        var values = Enumerable.Range(0, 64).Select(i => (ulong)i).ToArray();
        var tree = new Octree<ulong>(2, values);
        var encoded = tree.Data.ToArray();
        _ = tree.Get(1, 1, 1);
        _ = new OctreeSpan<ulong>(encoded).Get(1, 1, 1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        ulong sum = 0;
        for (var i = 0; i < 10_000; i++)
        {
            sum += tree.Get(i & 3, (i >> 2) & 3, (i >> 4) & 3);
            sum += new OctreeSpan<ulong>(encoded).Get(i & 3, (i >> 2) & 3, (i >> 4) & 3);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.NotEqual(0ul, sum);
        Assert.Equal(0, allocated);
    }

    private static T[] CreateValues<T>(Func<int, T> factory) where T : unmanaged =>
        Enumerable.Range(0, 8).Select(factory).ToArray();

    private static void AssertRoundTrip<T>(T[] values) where T : unmanaged
    {
        var levels = values.Length == 8 ? 1 : 2;
        var tree = new Octree<T>(levels, values);
        var decoded = new T[values.Length];
        tree.CopyTo(decoded);

        Assert.Equal(values, decoded);
        Assert.True(tree.AsSpan().IsWellFormed());
        Assert.Equal(values[0], tree[0, 0, 0]);
        Assert.Equal(values[^1], tree[tree.SideLength - 1, tree.SideLength - 1, tree.SideLength - 1]);

        var restored = Octree<T>.FromEncoded(tree.Data.Span);
        Array.Clear(decoded);
        restored.CopyTo(decoded);
        Assert.Equal(values, decoded);

        var required = Octree<T>.GetRequiredSize(values, levels);
        var destination = new byte[required];
        Assert.True(Octree<T>.TryBuild(values, levels, destination, out var written));
        Assert.Equal(required, written);
        Assert.True(new OctreeSpan<T>(destination).IsWellFormed());
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Struct8(byte Value);
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Struct16(ushort Value);
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Struct32(uint Value);
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Struct64(ulong Value);
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Struct128(ulong Low, ulong High);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Struct24(byte A, byte B, byte C);
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Struct96(ulong A, uint B);
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Struct192(ulong A, ulong B, ulong C);
}
