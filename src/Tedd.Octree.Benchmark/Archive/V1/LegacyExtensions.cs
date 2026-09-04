using System.Runtime.CompilerServices;

namespace Tedd.Octree.Benchmark.Archive.V1.Legacy;

// Local equivalents of the Tedd.BitUtils 1.0.5 and Tedd.SpanUtils 1.0.8
// operations used by the implementation archived from commit 3ac31f8.
internal static class LegacyExtensions
{
    private static readonly byte[] BitReverseLookup = CreateBitReverseLookup();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBitSet(this ref byte value, int bit) => (value & (1 << bit)) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBit(this ref byte value, int bit, bool set)
    {
        if (set)
            value = (byte)(value | (1 << bit));
        else
            value = (byte)(value & ~(1 << bit));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReverseBitsCopy(this ref uint value)
    {
        return BitReverseLookup[value >> 24] |
               ((uint)BitReverseLookup[(value >> 16) & 0xFF] << 8) |
               ((uint)BitReverseLookup[(value >> 8) & 0xFF] << 16) |
               ((uint)BitReverseLookup[value & 0xFF] << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte MoveReadByte(this ref Span<byte> data)
    {
        var value = data[0];
        data = data.Slice(1);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadSize(this ref Span<byte> data, out int bytesRead)
    {
        bytesRead = (data[0] >> 6) + 1;
        var value = (uint)(data[0] & 0x3F);
        for (var i = 1; i < bytesRead; i++)
            value = (value << 8) | data[i];

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint MoveReadSize(this ref Span<byte> data, out int bytesRead)
    {
        var value = data.ReadSize(out bytesRead);
        data = data.Slice(bytesRead);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MoveSize(this ref Span<byte> data, int count)
    {
        for (var i = 0; i < count; i++)
            data = data.Slice((data[0] >> 6) + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Move(this ref Span<byte> data, int count) => data = data.Slice(count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveWrite(this ref Span<byte> data, byte value)
    {
        data[0] = value;
        data = data.Slice(1);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MeasureWriteSize(this uint value)
    {
        if (value < 1u << 6)
            return 1;
        if (value < 1u << 14)
            return 2;
        if (value < 1u << 22)
            return 3;
        if (value < 1u << 30)
            return 4;

        throw new ArgumentOutOfRangeException(nameof(value), "The legacy size encoding is limited to 30-bit values.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveWriteSize(this ref Span<byte> data, uint value)
    {
        var byteCount = value.MeasureWriteSize();
        for (var i = byteCount - 1; i > 0; i--)
        {
            data[i] = (byte)value;
            value >>= 8;
        }

        data[0] = (byte)(value | ((uint)(byteCount - 1) << 6));
        data = data.Slice(byteCount);
        return byteCount;
    }

    private static byte[] CreateBitReverseLookup()
    {
        var lookup = new byte[256];
        for (var value = 0; value < lookup.Length; value++)
        {
            var source = value;
            var reversed = 0;
            for (var bit = 0; bit < 8; bit++)
            {
                reversed = (reversed << 1) | (source & 1);
                source >>= 1;
            }

            lookup[value] = (byte)reversed;
        }

        return lookup;
    }
}
