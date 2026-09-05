namespace Tedd.Voxtree.Tests;

public class GenericFormatValidationTests
{
    [Fact]
    public void OverlongGenericValueVarint_IsRejected()
    {
        var canonical = new Octree<uint>(1, new uint[8]).Data.ToArray();
        var overlong = new byte[canonical.Length + 1];
        canonical.AsSpan(0, 3).CopyTo(overlong);
        overlong[3] = 0x80;
        overlong[4] = 0;

        Assert.False(OctreeSpan<uint>.TryCreate(overlong, out _));
        Assert.Throws<FormatException>(() => new OctreeSpan<uint>(overlong));
        Assert.Throws<FormatException>(() => Octree<uint>.FromEncoded(overlong));
    }

    [Fact]
    public void OverflowingUInt128Varint_IsRejected()
    {
        var values = new UInt128[8];
        Array.Fill(values, UInt128.MaxValue);
        var encoded = new Octree<UInt128>(1, values).Data.ToArray();
        encoded[^1] = 0x04;

        Assert.False(OctreeSpan<UInt128>.TryCreate(encoded, out _));
        Assert.Throws<FormatException>(() => new OctreeSpan<UInt128>(encoded));
        Assert.Throws<FormatException>(() => Octree<UInt128>.FromEncoded(encoded));
    }

    [Fact]
    public void InvalidGenericElementWidthCode_IsRejectedByHeaderValidation()
    {
        var encoded = new Octree<ulong>(1, new ulong[8]).Data.ToArray();
        encoded[2] = byte.MaxValue;

        Assert.False(OctreeSpan<ulong>.TryCreate(encoded, out _));
        Assert.Throws<FormatException>(() => new OctreeSpan<ulong>(encoded));
    }

    [Fact]
    public void EmptyByteChunk_UsesAValidUniformEncodingAtNonzeroLevels()
    {
        var chunk = OctreeChunk<byte>.Empty(2, 2);

        Assert.True(chunk.GetChannel(0).IsWellFormed());
        Assert.True(chunk.GetChannel(1).IsWellFormed());
        Assert.Equal(0, chunk.GetChannel(0).Get(3, 3, 3));

        var packet = new byte[chunk.SerializedLength];
        Assert.Equal(packet.Length, chunk.CopyEncodedTo(packet));
        var restored = OctreeChunk<byte>.FromEncoded(packet);
        Assert.Equal(0, restored.GetChannel(1).Get(3, 3, 3));
    }
}
