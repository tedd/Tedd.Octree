using OctreeModel = global::Tedd.Octree.Octree;
using OctreeView = global::Tedd.Octree.OctreeSpan;

namespace Tedd.Octree.Tests;

public sealed class OctreeFormatValidationTests
{
    [Fact]
    public void FormatVersionOne_HasStableGoldenEncodings()
    {
        var uniformZero = new OctreeModel(0, new uint[] { 0u }).Data.ToArray();
        var denseMaximum = new OctreeModel(0, new uint[] { uint.MaxValue }).Data.ToArray();
        var levelOne = new OctreeModel(1, new uint[] { 0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u }).Data.ToArray();

        Assert.Equal(new byte[] { 0x4F, 0x41, 0x00 }, uniformZero);
        Assert.Equal(new byte[] { 0x4F, 0x42, 0xFF, 0xFF, 0xFF, 0xFF }, denseMaximum);
        Assert.Equal(
            new byte[] { 0x4F, 0x44, 0xFF, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x09 },
            levelOne);
    }

    [Fact]
    public void UnsupportedFormatVersion_IsRejectedByEveryDecodeEntryPoint()
    {
        var encoded = new OctreeModel(0, new uint[] { 1u }).Data.ToArray();
        encoded[1] ^= 0x40;

        Assert.False(TryCreate(encoded));
        Assert.Throws<FormatException>(() => ConstructView(encoded));
        Assert.Throws<FormatException>(() => OctreeModel.FromEncoded(encoded));
    }

    [Fact]
    public void NonCanonicalOverlongVarint_IsRejected()
    {
        var canonical = new OctreeModel(0, new uint[] { 0u }).Data.ToArray();
        var overlong = new byte[] { canonical[0], canonical[1], 0x80, 0x00 };

        Assert.False(TryCreate(overlong));
        Assert.Throws<FormatException>(() => ConstructView(overlong));
        Assert.Throws<FormatException>(() => OctreeModel.FromEncoded(overlong));
    }

    [Fact]
    public void OverflowingVarint_IsRejected()
    {
        var overflowing = new byte[]
        {
            0x4F, 0x41, 0xFF, 0xFF, 0xFF, 0xFF, 0x10
        };

        Assert.False(TryCreate(overflowing));
        Assert.Throws<FormatException>(() => ConstructView(overflowing));
        Assert.Throws<FormatException>(() => OctreeModel.FromEncoded(overflowing));
    }

    private static bool TryCreate(byte[] encoded) => OctreeView.TryCreate(encoded, out _);

    private static void ConstructView(byte[] encoded) => _ = new OctreeView(encoded);
}
