using System;
using System.Runtime.CompilerServices;

namespace Tedd.Voxtree.Benchmark.Archive.MortonBefore;

/// <summary>Unsigned operations over the codec's exact-width primitive storage cells.</summary>
internal static class StorageInteger<T> where T : unmanaged, IEquatable<T>
{
    internal static readonly int BitCount = Unsafe.SizeOf<T>() * 8;
    internal static readonly int MaximumVarIntBytes = (BitCount + 6) / 7;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasMoreThanSevenBits(T value)
    {
        if (Unsafe.SizeOf<T>() == 16)
        {
            var wide = Unsafe.As<T, VoxelUInt128>(ref value);
            return wide.High != 0 || wide.Low >= 0x80;
        }
        return ToUInt64(value) >= 0x80;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte LowByte(T value)
    {
        if (Unsafe.SizeOf<T>() == 16)
            return (byte)Unsafe.As<T, VoxelUInt128>(ref value).Low;
        return (byte)ToUInt64(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T ShiftRightSeven(T value)
    {
        if (Unsafe.SizeOf<T>() == 16)
        {
            var wide = Unsafe.As<T, VoxelUInt128>(ref value);
            var shifted = new VoxelUInt128
            {
                Low = (wide.Low >> 7) | (wide.High << 57),
                High = wide.High >> 7
            };
            return Unsafe.As<VoxelUInt128, T>(ref shifted);
        }
        return FromUInt64(ToUInt64(value) >> 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryAppendSevenBits(ref T value, byte payload, int shift)
    {
        var remaining = BitCount - shift;
        if (remaining <= 0 || (remaining < 7 && payload >= (1 << remaining))) return false;
        if (Unsafe.SizeOf<T>() == 16)
        {
            var wide = Unsafe.As<T, VoxelUInt128>(ref value);
            if (shift < 64)
            {
                wide.Low |= (ulong)payload << shift;
                if (shift > 57) wide.High |= (ulong)payload >> (64 - shift);
            }
            else wide.High |= (ulong)payload << (shift - 64);
            value = Unsafe.As<VoxelUInt128, T>(ref wide);
            return true;
        }
        value = FromUInt64(ToUInt64(value) | ((ulong)payload << shift));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ToUInt64(T value) => Unsafe.SizeOf<T>() switch
    {
        1 => Unsafe.As<T, byte>(ref value),
        2 => Unsafe.As<T, ushort>(ref value),
        4 => Unsafe.As<T, uint>(ref value),
        8 => Unsafe.As<T, ulong>(ref value),
        _ => throw new InvalidOperationException("Unsupported storage width.")
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T FromUInt64(ulong value)
    {
        switch (Unsafe.SizeOf<T>())
        {
            case 1:
            {
                var narrowed = (byte)value;
                return Unsafe.As<byte, T>(ref narrowed);
            }
            case 2:
            {
                var narrowed = (ushort)value;
                return Unsafe.As<ushort, T>(ref narrowed);
            }
            case 4:
            {
                var narrowed = (uint)value;
                return Unsafe.As<uint, T>(ref narrowed);
            }
            case 8:
                return Unsafe.As<ulong, T>(ref value);
            default:
                throw new InvalidOperationException("Unsupported storage width.");
        }
    }
}
