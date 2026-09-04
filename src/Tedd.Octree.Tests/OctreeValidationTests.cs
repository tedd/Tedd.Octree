using OctreeModel = global::Tedd.Octree.Octree;
using OctreeView = global::Tedd.Octree.OctreeSpan;

namespace Tedd.Octree.Tests;

public sealed class OctreeValidationTests
{
    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 2, 8)]
    [InlineData(2, 4, 64)]
    [InlineData(3, 8, 512)]
    [InlineData(4, 16, 4096)]
    [InlineData(5, 32, 32768)]
    [InlineData(6, 64, 262144)]
    [InlineData(7, 128, 2097152)]
    [InlineData(8, 256, 16777216)]
    [InlineData(9, 512, 134217728)]
    public void Constructor_ComputesMetadataWithoutAllocatingVoxelStorage(int levels, int side, int count)
    {
        var tree = new OctreeModel(levels);

        Assert.Equal(levels, tree.Levels);
        Assert.Equal(side, tree.SideLength);
        Assert.Equal(count, tree.Count);
        Assert.False(tree.IsBuilt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidLevels_AreRejectedByAllBuildEntryPoints(int levels)
    {
        var source = new uint[1];
        var destination = new byte[64];

        Assert.Throws<ArgumentOutOfRangeException>(() => new OctreeModel(levels));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OctreeModel(levels, source));
        Assert.Throws<ArgumentOutOfRangeException>(() => OctreeModel.GetMaximumSize(levels));
        Assert.Throws<ArgumentOutOfRangeException>(() => OctreeModel.GetRequiredSize(source, levels));
        Assert.Throws<ArgumentOutOfRangeException>(() => OctreeModel.Build(source, levels, destination));
        Assert.Throws<ArgumentOutOfRangeException>(() => TryStaticBuild(source, levels, destination));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    [InlineData(1, 7)]
    [InlineData(1, 9)]
    [InlineData(2, 63)]
    [InlineData(2, 65)]
    public void SourceLengthMustExactlyMatchLevelVolume(int levels, int length)
    {
        var source = new uint[length];
        var destination = new byte[4096];
        var tree = new OctreeModel(levels);

        Assert.Throws<ArgumentException>(() => new OctreeModel(levels, source));
        Assert.Throws<ArgumentException>(() => tree.Build(source));
        Assert.Throws<ArgumentException>(() => OctreeModel.GetRequiredSize(source, levels));
        Assert.Throws<ArgumentException>(() => OctreeModel.Build(source, levels, destination));
        Assert.Throws<ArgumentException>(() => TryStaticBuild(source, levels, destination));
        Assert.False(tree.IsBuilt);
    }

    [Fact]
    public void OwnedTree_GetAndIndexerRejectEveryOutOfRangeAxis()
    {
        var tree = new OctreeModel(2, TestData.Coordinates(2));

        AssertOwnedCoordinatesRejected(tree, -1, 0, 0);
        AssertOwnedCoordinatesRejected(tree, 4, 0, 0);
        AssertOwnedCoordinatesRejected(tree, 0, -1, 0);
        AssertOwnedCoordinatesRejected(tree, 0, 4, 0);
        AssertOwnedCoordinatesRejected(tree, 0, 0, -1);
        AssertOwnedCoordinatesRejected(tree, 0, 0, 4);
        AssertOwnedCoordinatesRejected(tree, int.MinValue, 0, 0);
        AssertOwnedCoordinatesRejected(tree, int.MaxValue, 0, 0);
    }

    [Fact]
    public void SpanView_GetAndIndexerRejectEveryOutOfRangeAxis()
    {
        var encoded = new OctreeModel(2, TestData.Coordinates(2)).Data.ToArray();

        AssertViewCoordinatesRejected(encoded, -1, 0, 0);
        AssertViewCoordinatesRejected(encoded, 4, 0, 0);
        AssertViewCoordinatesRejected(encoded, 0, -1, 0);
        AssertViewCoordinatesRejected(encoded, 0, 4, 0);
        AssertViewCoordinatesRejected(encoded, 0, 0, -1);
        AssertViewCoordinatesRejected(encoded, 0, 0, 4);
        AssertViewCoordinatesRejected(encoded, int.MinValue, 0, 0);
        AssertViewCoordinatesRejected(encoded, int.MaxValue, 0, 0);
    }

    [Fact]
    public void DefaultSpanView_IsInvalidAndSafeToProbe()
    {
        OctreeView view = default;

        Assert.False(view.IsValid);
        Assert.False(view.IsWellFormed());
        Assert.False(view.TryGet(0, 0, 0, out var value));
        Assert.Equal(0u, value);
        Assert.False(view.Contains(0, 0, 0));
        Assert.True(view.Data.IsEmpty);
    }

    [Fact]
    public void EmptyAndBasicHeaderCorruption_AreRejected()
    {
        var encoded = new OctreeModel(2, TestData.Coordinates(2)).Data.ToArray();
        var corruptMagic = (byte[])encoded.Clone();
        corruptMagic[0] ^= 0xFF;

        Assert.Throws<FormatException>(() => ConstructView(Array.Empty<byte>()));
        Assert.Throws<FormatException>(() => ConstructView(corruptMagic));
        Assert.Throws<FormatException>(() => OctreeModel.FromEncoded(Array.Empty<byte>()));
        Assert.Throws<FormatException>(() => OctreeModel.FromEncoded(corruptMagic));
        Assert.False(TryCreate(Array.Empty<byte>()));
        Assert.False(TryCreate(corruptMagic));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TruncatedHeader_IsRejected(int length)
    {
        var encoded = new OctreeModel(3, TestData.XorShift(3)).Data.ToArray();

        Assert.False(IsWellFormedOrConstructible(encoded, length));
        Assert.Throws<FormatException>(() => DecodePrefix(encoded, length));
    }

    [Fact]
    public void TrailingBytes_AreNotPartOfAnEncoding()
    {
        var source = TestData.Coordinates(2);
        var encoded = new OctreeModel(2, source).Data.ToArray();
        var oversized = new byte[encoded.Length + 1];
        encoded.CopyTo(oversized, 0);
        oversized[^1] = 0x5A;

        Assert.False(IsWellFormed(oversized));
        Assert.False(TryGetFromView(oversized, 0, 0, 0, out _));
        Assert.Throws<FormatException>(() => OctreeModel.FromEncoded(oversized));
    }

    [Fact]
    public void FromEncoded_RestoresAllMetadataAndValues()
    {
        var source = TestData.Checkerboard(4);
        var original = new OctreeModel(4, source);
        var restored = OctreeModel.FromEncoded(original.Data.Span);

        Assert.True(restored.IsBuilt);
        Assert.Equal(original.Levels, restored.Levels);
        Assert.Equal(original.SideLength, restored.SideLength);
        Assert.Equal(original.Count, restored.Count);
        Assert.Equal(original.EncodedLength, restored.EncodedLength);
        Assert.True(original.Data.Span.SequenceEqual(restored.Data.Span));

        var decoded = new uint[source.Length];
        restored.CopyTo(decoded);
        Assert.Equal(source, decoded);
    }

    private static void AssertOwnedCoordinatesRejected(OctreeModel tree, int x, int y, int z)
    {
        Assert.False(tree.Contains(x, y, z));
        Assert.False(tree.TryGet(x, y, z, out var value));
        Assert.Equal(0u, value);
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Get(x, y, z));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadOwnedIndexer(tree, x, y, z));
    }

    private static void AssertViewCoordinatesRejected(byte[] encoded, int x, int y, int z)
    {
        var view = new OctreeView(encoded);
        Assert.False(view.Contains(x, y, z));
        Assert.False(view.TryGet(x, y, z, out var value));
        Assert.Equal(0u, value);
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadView(encoded, x, y, z));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadViewIndexer(encoded, x, y, z));
    }

    private static uint ReadOwnedIndexer(OctreeModel tree, int x, int y, int z) => tree[x, y, z];

    private static uint ReadView(byte[] encoded, int x, int y, int z) =>
        new OctreeView(encoded).Get(x, y, z);

    private static uint ReadViewIndexer(byte[] encoded, int x, int y, int z) =>
        new OctreeView(encoded)[x, y, z];

    private static void ConstructView(byte[] encoded) => _ = new OctreeView(encoded);

    private static bool TryCreate(byte[] encoded) => OctreeView.TryCreate(encoded, out _);

    private static bool IsWellFormed(byte[] encoded)
    {
        try
        {
            var view = new OctreeView(encoded);
            return view.IsWellFormed();
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsWellFormedOrConstructible(byte[] encoded, int length)
    {
        try
        {
            var view = new OctreeView(encoded.AsSpan(0, length));
            if (view.IsWellFormed())
                return true;

            return view.TryGet(0, 0, 0, out _);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void DecodePrefix(byte[] encoded, int length) =>
        OctreeModel.FromEncoded(encoded.AsSpan(0, length));

    private static bool TryGetFromView(byte[] encoded, int x, int y, int z, out uint value)
    {
        try
        {
            var view = new OctreeView(encoded);
            return view.TryGet(x, y, z, out value);
        }
        catch (FormatException)
        {
            value = default;
            return false;
        }
    }

    private static bool TryStaticBuild(uint[] source, int levels, byte[] destination) =>
        OctreeModel.TryBuild(source, levels, destination, out _);
}
