using System;
using System.Runtime.CompilerServices;

namespace Tedd.Voxtree.Benchmark.Archive.MortonBefore;

/// <summary>An allocation-free equality or raw-bit mask filter for <typeparamref name="T"/> values.</summary>
/// <remarks>The default filter matches all values. Masking is applied to the unmanaged bit representation.</remarks>
public readonly struct VoxelFilter<T> where T : unmanaged
{
    private readonly T _mask;
    private readonly T _expected;
    private readonly bool _negate;

    private VoxelFilter(T mask, T expected, bool negate)
    {
        VoxelType<T>.Validate();
        _mask = mask;
        _expected = BitwiseAnd(expected, mask);
        _negate = negate;
    }

    /// <summary>Matches every value.</summary>
    public static VoxelFilter<T> Any => default;
    /// <summary>Matches every value whose bit representation is not zero.</summary>
    public static VoxelFilter<T> NonZero => new(AllBitsSet(), default, true);
    /// <summary>Matches one exact bit representation.</summary>
    public static VoxelFilter<T> EqualTo(T value) => new(AllBitsSet(), value, false);
    /// <summary>Matches every bit representation except the specified value.</summary>
    public static VoxelFilter<T> NotEqualTo(T value) => new(AllBitsSet(), value, true);
    /// <summary>Matches values for which the masked bit representation equals the masked expected value.</summary>
    public static VoxelFilter<T> Masked(T mask, T expected) => new(mask, expected, false);

    /// <summary>Tests a value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches(T value) => BitwiseEquals(value, _mask, _expected) != _negate;

    internal int CountIn(ReadOnlySpan<T> values)
    {
        if (IsZero(_mask)) return _negate ? 0 : values.Length;
        var count = 0;
        foreach (var value in values)
            if (Matches(value)) count++;
        return count;
    }

    internal bool AnyIn(ReadOnlySpan<T> values)
    {
        if (IsZero(_mask)) return !values.IsEmpty && !_negate;
        foreach (var value in values)
            if (Matches(value)) return true;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool BitwiseEquals(T value, T mask, T expected)
    {
        switch (Unsafe.SizeOf<T>())
        {
            case 1:
                return (Unsafe.As<T, byte>(ref value) & Unsafe.As<T, byte>(ref mask)) == Unsafe.As<T, byte>(ref expected);
            case 2:
                return (Unsafe.As<T, ushort>(ref value) & Unsafe.As<T, ushort>(ref mask)) == Unsafe.As<T, ushort>(ref expected);
            case 4:
                return (Unsafe.As<T, uint>(ref value) & Unsafe.As<T, uint>(ref mask)) == Unsafe.As<T, uint>(ref expected);
            case 8:
                return (Unsafe.As<T, ulong>(ref value) & Unsafe.As<T, ulong>(ref mask)) == Unsafe.As<T, ulong>(ref expected);
            case 16:
            {
                var actual128 = Unsafe.As<T, VoxelUInt128>(ref value);
                var mask128 = Unsafe.As<T, VoxelUInt128>(ref mask);
                var expected128 = Unsafe.As<T, VoxelUInt128>(ref expected);
                return (actual128.Low & mask128.Low) == expected128.Low &&
                       (actual128.High & mask128.High) == expected128.High;
            }
            default:
                VoxelType<T>.Validate();
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T BitwiseAnd(T left, T right)
    {
        switch (Unsafe.SizeOf<T>())
        {
            case 1:
            {
                var result = (byte)(Unsafe.As<T, byte>(ref left) & Unsafe.As<T, byte>(ref right));
                return Unsafe.As<byte, T>(ref result);
            }
            case 2:
            {
                var result = (ushort)(Unsafe.As<T, ushort>(ref left) & Unsafe.As<T, ushort>(ref right));
                return Unsafe.As<ushort, T>(ref result);
            }
            case 4:
            {
                var result = Unsafe.As<T, uint>(ref left) & Unsafe.As<T, uint>(ref right);
                return Unsafe.As<uint, T>(ref result);
            }
            case 8:
            {
                var result = Unsafe.As<T, ulong>(ref left) & Unsafe.As<T, ulong>(ref right);
                return Unsafe.As<ulong, T>(ref result);
            }
            case 16:
            {
                var left128 = Unsafe.As<T, VoxelUInt128>(ref left);
                var right128 = Unsafe.As<T, VoxelUInt128>(ref right);
                var result = new VoxelUInt128 { Low = left128.Low & right128.Low, High = left128.High & right128.High };
                return Unsafe.As<VoxelUInt128, T>(ref result);
            }
            default:
                VoxelType<T>.Validate();
                return default;
        }
    }

    private static bool IsZero(T value)
    {
        var zero = default(T);
        return BitwiseEquals(value, AllBitsSet(), zero);
    }

    private static T AllBitsSet()
    {
        switch (Unsafe.SizeOf<T>())
        {
            case 1:
            {
                var value = byte.MaxValue;
                return Unsafe.As<byte, T>(ref value);
            }
            case 2:
            {
                var value = ushort.MaxValue;
                return Unsafe.As<ushort, T>(ref value);
            }
            case 4:
            {
                var value = uint.MaxValue;
                return Unsafe.As<uint, T>(ref value);
            }
            case 8:
            {
                var value = ulong.MaxValue;
                return Unsafe.As<ulong, T>(ref value);
            }
            case 16:
            {
                var value = new VoxelUInt128 { Low = ulong.MaxValue, High = ulong.MaxValue };
                return Unsafe.As<VoxelUInt128, T>(ref value);
            }
            default:
                VoxelType<T>.Validate();
                return default;
        }
    }
}

/// <summary>A voxel coordinate and its generic channel value.</summary>
public readonly struct VoxelHit<T> where T : unmanaged
{
    /// <summary>Creates a voxel result.</summary>
    public VoxelHit(int x, int y, int z, T value)
    {
        X = x;
        Y = y;
        Z = z;
        Value = value;
    }
    /// <summary>X coordinate.</summary>
    public int X { get; }
    /// <summary>Y coordinate.</summary>
    public int Y { get; }
    /// <summary>Z coordinate.</summary>
    public int Z { get; }
    /// <summary>The stored channel value.</summary>
    public T Value { get; }
}
