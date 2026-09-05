using System;
using System.Runtime.CompilerServices;

namespace Tedd.Octree;

/// <summary>An allocation-free equality or inequality filter over selected value bits.</summary>
/// <remarks>The default filter matches all values. No delegate or boxing is required.</remarks>
public readonly struct VoxelFilter
{
    private readonly uint _mask;
    private readonly uint _expected;
    private readonly bool _negate;

    private VoxelFilter(uint mask, uint expected, bool negate)
    {
        _mask = mask; _expected = expected & mask; _negate = negate;
    }

    /// <summary>Matches every value, including zero.</summary>
    public static VoxelFilter Any => default;
    /// <summary>Matches every nonzero value; useful when zero means air.</summary>
    public static VoxelFilter NonZero => new(uint.MaxValue, 0, true);
    /// <summary>Matches one exact value.</summary>
    public static VoxelFilter EqualTo(uint value) => new(uint.MaxValue, value, false);
    /// <summary>Matches every value except the specified value.</summary>
    public static VoxelFilter NotEqualTo(uint value) => new(uint.MaxValue, value, true);
    /// <summary>Matches values for which (value &amp; mask) equals (expected &amp; mask).</summary>
    public static VoxelFilter Masked(uint mask, uint expected) => new(mask, expected, false);
    /// <summary>Tests a value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches(uint value) => ((value & _mask) == _expected) != _negate;

    internal int CountIn(ReadOnlySpan<uint> values)
    {
        if (_mask == 0)
            return _negate ? 0 : values.Length;
#if NET10_0_OR_GREATER
        if (_mask == uint.MaxValue)
        {
            var equal = values.Count(_expected);
            return _negate ? values.Length - equal : equal;
        }
#endif
        var count = 0;
        foreach (var value in values)
            if (Matches(value)) count++;
        return count;
    }

    internal bool AnyIn(ReadOnlySpan<uint> values)
    {
        if (_mask == 0)
            return !values.IsEmpty && !_negate;
#if NET10_0_OR_GREATER
        if (_mask == uint.MaxValue)
            return _negate ? values.IndexOfAnyExcept(_expected) >= 0 : values.IndexOf(_expected) >= 0;
#endif
        foreach (var value in values)
            if (Matches(value))
                return true;
        return false;
    }
}
