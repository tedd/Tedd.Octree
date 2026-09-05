using System.Runtime.InteropServices;
using OctreeModel = global::Tedd.Voxtree.Octree;
using OctreeView = global::Tedd.Voxtree.OctreeSpan;

namespace Tedd.Voxtree.Tests;

public sealed class OctreeBufferTests
{
    private const byte Sentinel = 0xA5;

    [Fact]
    public void StaticBuild_ExactBufferMatchesOwnedAndTryBuild()
    {
        var source = TestData.XorShift(4);
        var required = OctreeModel.GetRequiredSize(source, 4);
        var built = new byte[required];
        var tried = new byte[required];

        var written = OctreeModel.Build(source, 4, built);
        var success = OctreeModel.TryBuild(source, 4, tried, out var tryWritten);
        var owned = new OctreeModel(4, source);

        Assert.Equal(required, written);
        Assert.True(success);
        Assert.Equal(required, tryWritten);
        Assert.Equal(built, tried);
        Assert.True(built.AsSpan().SequenceEqual(owned.Data.Span));

        var view = new OctreeView(built);
        Assert.True(view.IsValid);
        Assert.True(view.IsWellFormed());
        Assert.Equal(required, view.EncodedLength);
    }

    [Fact]
    public void Serialization_IsDeterministicAcrossRepeatedBuilds()
    {
        var source = TestData.Coordinates(3);
        var required = OctreeModel.GetRequiredSize(source, 3);
        var first = new byte[required];
        var second = new byte[required];
        var third = new OctreeModel(3, source);

        Assert.Equal(required, OctreeModel.Build(source, 3, first));
        Assert.Equal(required, OctreeModel.Build(source, 3, second));

        Assert.Equal(first, second);
        Assert.True(first.AsSpan().SequenceEqual(third.Data.Span));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void MaximumSize_HoldsAnyPatternWithoutSizingPass(int levels)
    {
        var source = TestData.XorShift(levels);
        var maximum = OctreeModel.GetMaximumSize(levels);
        var required = OctreeModel.GetRequiredSize(source, levels);
        var destination = new byte[maximum];

        Assert.True(required <= maximum);
        Assert.True(OctreeModel.TryBuild(source, levels, destination, out var written));
        Assert.Equal(required, written);
        Assert.True(new OctreeView(destination.AsSpan(0, written)).IsWellFormed());
    }

    [Fact]
    public void StaticBuild_OversizedAndSlicedBufferPreservesSentinels()
    {
        var source = TestData.Checkerboard(3);
        var required = OctreeModel.GetRequiredSize(source, 3);
        var storage = new byte[required + 19];
        Array.Fill(storage, Sentinel);
        var destination = storage.AsSpan(7, required + 5);

        var written = OctreeModel.Build(source, 3, destination);

        Assert.Equal(required, written);
        Assert.All(storage.AsSpan(0, 7).ToArray(), value => Assert.Equal(Sentinel, value));
        Assert.All(storage.AsSpan(7 + required).ToArray(), value => Assert.Equal(Sentinel, value));

        var view = new OctreeView(destination[..written]);
        var decoded = new uint[source.Length];
        view.CopyTo(decoded);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void TryBuild_OversizedAndSlicedBufferPreservesSentinels()
    {
        var source = TestData.Coordinates(3);
        var required = OctreeModel.GetRequiredSize(source, 3);
        var storage = new byte[required + 23];
        Array.Fill(storage, Sentinel);
        var destination = storage.AsSpan(11, required + 3);

        Assert.True(OctreeModel.TryBuild(source, 3, destination, out var written));

        Assert.Equal(required, written);
        Assert.All(storage.AsSpan(0, 11).ToArray(), value => Assert.Equal(Sentinel, value));
        Assert.All(storage.AsSpan(11 + required).ToArray(), value => Assert.Equal(Sentinel, value));

        var view = new OctreeView(destination[..written]);
        Assert.Equal(source[0], view[0, 0, 0]);
        Assert.Equal(source[^1], view[7, 7, 7]);
    }

    [Fact]
    public void ShortDestination_IsRejected()
    {
        var source = TestData.XorShift(3);
        var required = OctreeModel.GetRequiredSize(source, 3);
        var shortBuffer = new byte[required - 1];
        Array.Fill(shortBuffer, Sentinel);

        Assert.False(OctreeModel.TryBuild(source, 3, shortBuffer, out var written));
        Assert.Equal(0, written);

        Assert.Throws<ArgumentException>(() => BuildIntoShortBuffer(source, shortBuffer));
    }

    [Fact]
    public void StaticBuildAndRead_WorkWithStackAllocatedSlicedBuffers()
    {
        ReadOnlySpan<uint> source = stackalloc uint[]
        {
            0u, 0x3Fu, 0x40u, 0x7Fu, 0x80u, 0x3FFFu, 0x4000u, uint.MaxValue
        };
        var required = OctreeModel.GetRequiredSize(source, 1);
        Span<byte> storage = stackalloc byte[256];
        storage.Fill(Sentinel);
        var destination = storage.Slice(9, required);

        var written = OctreeModel.Build(source, 1, destination);

        Assert.Equal(required, written);
        Assert.True(storage[..9].IndexOfAnyExcept(Sentinel) < 0);
        Assert.True(storage[(9 + required)..].IndexOfAnyExcept(Sentinel) < 0);

        var view = new OctreeView(destination);
        Span<uint> decoded = stackalloc uint[8];
        view.CopyTo(decoded);
        Assert.True(source.SequenceEqual(decoded));
    }

    [Fact]
    public void CopyTo_ExactOversizedAndSlicedDestinationsPreserveUnwrittenData()
    {
        var source = TestData.XorShift(3);
        var tree = new OctreeModel(3, source);
        var storage = new uint[source.Length + 13];
        Array.Fill(storage, uint.MaxValue);
        var destination = storage.AsSpan(5, source.Length + 3);

        tree.CopyTo(destination);

        Assert.All(storage.AsSpan(0, 5).ToArray(), value => Assert.Equal(uint.MaxValue, value));
        Assert.True(source.AsSpan().SequenceEqual(destination[..source.Length]));
        Assert.All(storage.AsSpan(5 + source.Length).ToArray(), value => Assert.Equal(uint.MaxValue, value));

        Array.Fill(storage, uint.MaxValue);
        Assert.True(tree.AsSpan().TryCopyTo(destination));
        Assert.True(source.AsSpan().SequenceEqual(destination[..source.Length]));
        Assert.All(storage.AsSpan(5 + source.Length).ToArray(), value => Assert.Equal(uint.MaxValue, value));
    }

    [Fact]
    public void CopyTo_ShortDestinationIsRejectedAtomically()
    {
        var source = TestData.Coordinates(2);
        var tree = new OctreeModel(2, source);
        var destination = new uint[source.Length - 1];
        Array.Fill(destination, uint.MaxValue);

        Assert.False(tree.TryCopyTo(destination));
        Assert.All(destination, value => Assert.Equal(uint.MaxValue, value));
        Assert.Throws<ArgumentException>(() => tree.CopyTo(destination));
        Assert.All(destination, value => Assert.Equal(uint.MaxValue, value));

        var encoded = tree.Data.ToArray();
        Assert.False(TryCopyView(encoded, destination));
        Assert.All(destination, value => Assert.Equal(uint.MaxValue, value));
        Assert.Throws<ArgumentException>(() => CopyView(encoded, destination));
        Assert.All(destination, value => Assert.Equal(uint.MaxValue, value));
    }

    [Fact]
    public void FromEncoded_CopiesInputWhileOctreeSpanAliasesIt()
    {
        var source = TestData.Coordinates(2);
        var encoded = new OctreeModel(2, source).Data.ToArray();
        var owned = OctreeModel.FromEncoded(encoded);
        var ownedSnapshot = owned.Data.ToArray();
        var view = new OctreeView(encoded);
        var aliasedData = view.Data;
        var originalFirstByte = encoded[0];
        var changedFirstByte = (byte)(originalFirstByte ^ 0xFF);

        encoded[0] = changedFirstByte;

        Assert.Equal(changedFirstByte, aliasedData[0]);
        Assert.True(ownedSnapshot.AsSpan().SequenceEqual(owned.Data.Span));

        encoded[0] = originalFirstByte;
        Assert.Equal(source[0], view[0, 0, 0]);
        Assert.Equal(source[^1], view[3, 3, 3]);
    }

    [Fact]
    public void BuildSourceAndDestination_CannotOverlap()
    {
        const int levels = 1;
        var values = TestData.Coordinates(levels);
        var sourceByteLength = values.Length * sizeof(uint);
        var required = OctreeModel.GetRequiredSize(values, levels);
        var storage = new byte[Math.Max(sourceByteLength, required)];
        values.AsSpan().CopyTo(MemoryMarshal.Cast<byte, uint>(storage.AsSpan(0, sourceByteLength)));
        var snapshot = (byte[])storage.Clone();

        Assert.Throws<ArgumentException>(() => TryBuildOverlapping(storage, required, values.Length));
        Assert.Equal(snapshot, storage);
        Assert.Throws<ArgumentException>(() => BuildOverlapping(storage, required, values.Length));
        Assert.Equal(snapshot, storage);

        var disjoint = new byte[sourceByteLength + required];
        values.AsSpan().CopyTo(MemoryMarshal.Cast<byte, uint>(disjoint.AsSpan(0, sourceByteLength)));
        Assert.Equal(required, BuildDisjoint(disjoint, sourceByteLength, required, values.Length));
        Assert.True(new OctreeView(disjoint.AsSpan(sourceByteLength, required)).IsWellFormed());
    }

    [Fact]
    public void DecodeDestination_CannotOverlapEncodedStorage()
    {
        var source = TestData.Checkerboard(2);
        var required = OctreeModel.GetRequiredSize(source, 2);
        var storage = new byte[Math.Max(required, source.Length * sizeof(uint)) + sizeof(uint)];
        var written = OctreeModel.Build(source, 2, storage);
        var snapshot = (byte[])storage.Clone();
        var destination = MemoryMarshal.Cast<byte, uint>(storage.AsSpan(0, source.Length * sizeof(uint)));
        var view = new OctreeView(storage.AsSpan(0, written));

        Assert.False(view.TryCopyTo(destination));
        Assert.Equal(snapshot, storage);
        Assert.Throws<ArgumentException>(() => CopyOverlapping(storage, written, source.Length));
        Assert.Equal(snapshot, storage);
    }

    private static int BuildIntoShortBuffer(uint[] source, byte[] destination) =>
        OctreeModel.Build(source, 3, destination);

    private static bool TryBuildOverlapping(byte[] storage, int encodedLength, int valueCount)
    {
        var source = MemoryMarshal.Cast<byte, uint>(storage.AsSpan(0, valueCount * sizeof(uint)));
        return OctreeModel.TryBuild(source, 1, storage.AsSpan(0, encodedLength), out _);
    }

    private static int BuildOverlapping(byte[] storage, int encodedLength, int valueCount)
    {
        var source = MemoryMarshal.Cast<byte, uint>(storage.AsSpan(0, valueCount * sizeof(uint)));
        return OctreeModel.Build(source, 1, storage.AsSpan(0, encodedLength));
    }

    private static int BuildDisjoint(byte[] storage, int destinationOffset, int encodedLength, int valueCount)
    {
        var source = MemoryMarshal.Cast<byte, uint>(storage.AsSpan(0, valueCount * sizeof(uint)));
        return OctreeModel.Build(source, 1, storage.AsSpan(destinationOffset, encodedLength));
    }

    private static bool TryCopyView(byte[] encoded, uint[] destination)
    {
        var view = new OctreeView(encoded);
        return view.TryCopyTo(destination);
    }

    private static void CopyView(byte[] encoded, uint[] destination)
    {
        var view = new OctreeView(encoded);
        view.CopyTo(destination);
    }

    private static void CopyOverlapping(byte[] storage, int encodedLength, int valueCount)
    {
        var view = new OctreeView(storage.AsSpan(0, encodedLength));
        var destination = MemoryMarshal.Cast<byte, uint>(storage.AsSpan(0, valueCount * sizeof(uint)));
        view.CopyTo(destination);
    }
}
