using OctreeModel = global::Tedd.Octree.Octree;
using OctreeView = global::Tedd.Octree.OctreeSpan;

namespace Tedd.Octree.Tests;

public sealed class OctreeRoundTripTests
{
    public static TheoryData<int> SupportedLevels => new()
    {
        0, 1, 2, 3, 4, 5, 6
    };

    [Theory]
    [MemberData(nameof(SupportedLevels))]
    public void CoordinatePattern_RoundTripsEveryCell(int levels)
    {
        var source = TestData.Coordinates(levels);
        var tree = new OctreeModel(levels, source);

        Assert.True(tree.IsBuilt);
        Assert.Equal(levels, tree.Levels);
        Assert.Equal(1 << levels, tree.SideLength);
        Assert.Equal(source.Length, tree.Count);
        Assert.Equal(tree.Data.Length, tree.EncodedLength);
        Assert.NotEqual(0, tree.EncodedLength);

        AssertOwnedTree(tree, source);

        var view = tree.AsSpan();
        Assert.True(view.IsValid);
        Assert.True(view.IsWellFormed());
        Assert.Equal(tree.Levels, view.Levels);
        Assert.Equal(tree.SideLength, view.SideLength);
        Assert.Equal(tree.Count, view.Count);
        Assert.Equal(tree.EncodedLength, view.EncodedLength);
        Assert.True(tree.Data.Span.SequenceEqual(view.Data));
        AssertView(view, source);
    }

    [Theory]
    [MemberData(nameof(SupportedLevels))]
    public void ConstantPattern_RoundTripsAndCompresses(int levels)
    {
        var source = TestData.Constant(levels, uint.MaxValue);
        var tree = new OctreeModel(levels, source);
        var side = tree.SideLength;

        Assert.Equal(uint.MaxValue, tree[0, 0, 0]);
        Assert.Equal(uint.MaxValue, tree[side - 1, side - 1, side - 1]);
        Assert.Equal(uint.MaxValue, tree.Get(side / 2, side / 2, side / 2));

        if (levels > 0)
            Assert.True(tree.EncodedLength < source.Length * sizeof(uint));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void AdversarialCheckerboard_RoundTrips(int levels)
    {
        var source = TestData.Checkerboard(levels);
        var tree = new OctreeModel(levels, source);

        AssertOwnedTree(tree, source);
        AssertView(tree.AsSpan(), source);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void DeterministicPseudoRandomPattern_RoundTrips(int levels)
    {
        var source = TestData.XorShift(levels);
        var tree = new OctreeModel(levels, source);

        AssertOwnedTree(tree, source);
        AssertView(tree.AsSpan(), source);
    }

    [Fact]
    public void UnsignedIntegerEncodingBoundaries_RoundTripExactly()
    {
        uint[] boundaries =
        {
            0u, 1u, 0x3Fu, 0x40u, 0x7Fu, 0x80u,
            0x3FFFu, 0x4000u, 0x7FFFu, 0x8000u,
            0x1F_FFFFu, 0x20_0000u, 0x0FFF_FFFFu, 0x1000_0000u,
            uint.MaxValue - 1u, uint.MaxValue
        };

        var source = new uint[TestData.CountFor(2)];
        for (var i = 0; i < source.Length; i++)
            source[i] = boundaries[i % boundaries.Length];

        var tree = new OctreeModel(2, source);

        AssertOwnedTree(tree, source);
        AssertView(tree.AsSpan(), source);
        AssertDecodedCopy(tree, source);
        AssertDecodedCopy(tree.AsSpan(), source);
    }

    [Fact]
    public void ConstructorAndBuild_DoNotRetainSourceValues()
    {
        var constructorSource = TestData.Coordinates(2);
        var constructorExpected = (uint[])constructorSource.Clone();
        var constructed = new OctreeModel(2, constructorSource);
        Array.Fill(constructorSource, 0xDEAD_BEEFu);
        AssertOwnedTree(constructed, constructorExpected);

        var buildSource = TestData.XorShift(2);
        var buildExpected = (uint[])buildSource.Clone();
        var built = new OctreeModel(2);
        built.Build(buildSource);
        Array.Fill(buildSource, 0xBAD0_C0DEu);
        AssertOwnedTree(built, buildExpected);
    }

    [Fact]
    public void Rebuild_ReplacesAllLogicalAndEncodedData()
    {
        var first = TestData.Constant(3, 17u);
        var second = TestData.Coordinates(3);
        var tree = new OctreeModel(3, first);
        var firstEncoding = tree.Data.ToArray();

        tree.Build(second);

        AssertOwnedTree(tree, second);
        Assert.False(firstEncoding.AsSpan().SequenceEqual(tree.Data.Span));
    }

    [Fact]
    public void UnbuiltTree_ExposesMetadataButRejectsDataOperations()
    {
        var tree = new OctreeModel(4);

        Assert.False(tree.IsBuilt);
        Assert.Equal(4, tree.Levels);
        Assert.Equal(16, tree.SideLength);
        Assert.Equal(TestData.CountFor(4), tree.Count);
        Assert.Equal(0, tree.EncodedLength);
        Assert.True(tree.Data.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => tree.Get(0, 0, 0));
        Assert.Throws<InvalidOperationException>(() => CopyUnbuilt(tree));
        Assert.Throws<InvalidOperationException>(() => ViewUnbuilt(tree));
    }

    [Fact]
    public void PublicFormatConstants_AreSaneAndStable()
    {
        Assert.Equal(9, OctreeModel.MaxLevels);
        Assert.Equal(1, OctreeModel.FormatVersion);
        Assert.InRange(OctreeModel.FormatVersion, 1, 3);
    }

    private static void AssertOwnedTree(OctreeModel tree, uint[] expected)
    {
        var side = tree.SideLength;

        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
        {
            var value = expected[TestData.Index(side, x, y, z)];
            Assert.Equal(value, tree.Get(x, y, z));
            Assert.Equal(value, tree[x, y, z]);
            Assert.True(tree.TryGet(x, y, z, out var actual));
            Assert.Equal(value, actual);
            Assert.True(tree.Contains(x, y, z));
        }
    }

    private static void AssertView(OctreeView view, uint[] expected)
    {
        var side = view.SideLength;

        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
        {
            var value = expected[TestData.Index(side, x, y, z)];
            Assert.Equal(value, view.Get(x, y, z));
            Assert.Equal(value, view[x, y, z]);
            Assert.True(view.TryGet(x, y, z, out var actual));
            Assert.Equal(value, actual);
            Assert.True(view.Contains(x, y, z));
        }
    }

    private static void AssertDecodedCopy(OctreeModel tree, uint[] expected)
    {
        var actual = new uint[expected.Length];
        tree.CopyTo(actual);
        Assert.Equal(expected, actual);

        Array.Fill(actual, uint.MaxValue);
        Assert.True(tree.TryCopyTo(actual));
        Assert.Equal(expected, actual);
    }

    private static void AssertDecodedCopy(OctreeView view, uint[] expected)
    {
        var actual = new uint[expected.Length];
        view.CopyTo(actual);
        Assert.Equal(expected, actual);

        Array.Fill(actual, uint.MaxValue);
        Assert.True(view.TryCopyTo(actual));
        Assert.Equal(expected, actual);
    }

    private static void CopyUnbuilt(OctreeModel tree)
    {
        var destination = new uint[tree.Count];
        tree.CopyTo(destination);
    }

    private static void ViewUnbuilt(OctreeModel tree) => tree.AsSpan();
}
