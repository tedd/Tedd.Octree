using OctreeModel = global::Tedd.Voxtree.Octree;
using OctreeView = global::Tedd.Voxtree.OctreeSpan;

namespace Tedd.Voxtree.Tests;

public sealed class OctreeStructuralValidationTests
{
    [Fact]
    public void ReservedStorageKind_IsRejectedByHeaderValidation()
    {
        byte[] encoded = { 0x4F, 0x43, 0x00 };

        Assert.False(OctreeView.TryCreate(encoded, out _));
        Assert.Throws<FormatException>(() => Construct(encoded));
        Assert.Throws<FormatException>(() => OctreeModel.FromEncoded(encoded));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    public void InvalidRootNodeLength_IsRejectedByHeaderValidation(byte rootLength)
    {
        byte[] encoded =
        {
            0x4F, 0x44, 0xFF, 0x00, 0x01, 0x02,
            0x03, 0x04, 0x05, 0x06, 0x07, rootLength
        };

        Assert.False(OctreeView.TryCreate(encoded, out _));
        Assert.Throws<FormatException>(() => Construct(encoded));
        Assert.Throws<FormatException>(() => OctreeModel.FromEncoded(encoded));
    }

    [Fact]
    public void ZeroChildDistance_IsStructurallyRejected()
    {
        byte[] encoded =
        {
            0x4F, 0x48, 0xFE, 0x00, 0x01, 0x01,
            0x01, 0x01, 0x01, 0x01, 0x01, 0x09
        };

        AssertStructurallyRejected(encoded);
    }

    [Fact]
    public void CollapsibleNode_IsStructurallyRejected()
    {
        byte[] encoded =
        {
            0x4F, 0x44, 0xFF, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x09
        };

        AssertStructurallyRejected(encoded);
    }

    [Fact]
    public void UnreachableGap_IsStructurallyRejected()
    {
        byte[] encoded =
        {
            0x4F, 0x44, 0x7B, 0xFF, 0x00, 0x01, 0x02,
            0x03, 0x04, 0x05, 0x06, 0x07, 0x09
        };

        AssertStructurallyRejected(encoded);
    }

    [Fact]
    public void MaximumLevelUniformViewSupportsCornerLookupsWithoutDenseSource()
    {
        byte[] encoded = { 0x4F, 0x65, 0x2A };
        var view = new OctreeView(encoded);

        Assert.Equal(9, view.Levels);
        Assert.Equal(512, view.SideLength);
        Assert.Equal(134_217_728, view.Count);
        Assert.True(view.IsWellFormed());
        Assert.Equal(42u, view.Get(0, 0, 0));
        Assert.Equal(42u, view.Get(511, 511, 511));
    }

    [Fact]
    public void DeterministicByteMutationsNeverEscapeTryContracts()
    {
        var original = new OctreeModel(3, TestData.SparseOctants(3)).Data.ToArray();

        for (var index = 0; index < original.Length; index++)
        {
            var mutated = (byte[])original.Clone();
            mutated[index] ^= 0x5A;
            ProbeWithoutUnexpectedException(mutated);
        }

        for (var length = 0; length < original.Length; length++)
            ProbeWithoutUnexpectedException(original.AsSpan(0, length).ToArray());
    }

    private static void AssertStructurallyRejected(byte[] encoded)
    {
        Assert.True(OctreeView.TryCreate(encoded, out var view));
        Assert.False(view.IsWellFormed());

        var destination = new uint[view.Count];
        Array.Fill(destination, uint.MaxValue);
        Assert.False(view.TryCopyTo(destination));
        Assert.All(destination, value => Assert.Equal(uint.MaxValue, value));
        Assert.Throws<FormatException>(() => Copy(encoded));
        Assert.Throws<FormatException>(() => OctreeModel.FromEncoded(encoded));

        for (var x = 0; x < view.SideLength; x++)
        for (var y = 0; y < view.SideLength; y++)
        for (var z = 0; z < view.SideLength; z++)
            _ = view.TryGet(x, y, z, out _);
    }

    private static void ProbeWithoutUnexpectedException(byte[] encoded)
    {
        if (!OctreeView.TryCreate(encoded, out var view))
            return;

        _ = view.IsWellFormed();
        _ = view.TryGet(0, 0, 0, out _);

        var destination = new uint[view.Count];
        Array.Fill(destination, uint.MaxValue);
        var copied = view.TryCopyTo(destination);
        if (!copied)
            Assert.All(destination, value => Assert.Equal(uint.MaxValue, value));
    }

    private static void Construct(byte[] encoded) => _ = new OctreeView(encoded);

    private static void Copy(byte[] encoded)
    {
        var view = new OctreeView(encoded);
        view.CopyTo(new uint[view.Count]);
    }
}
