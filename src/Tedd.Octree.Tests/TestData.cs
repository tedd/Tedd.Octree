namespace Tedd.Octree.Tests;

internal static class TestData
{
    internal static uint[] Constant(int levels, uint value)
    {
        var values = new uint[CountFor(levels)];
        Array.Fill(values, value);
        return values;
    }

    internal static uint[] Coordinates(int levels)
    {
        var side = 1 << levels;
        var values = new uint[CountFor(levels)];

        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
        {
            values[Index(side, x, y, z)] =
                ((uint)x * 0x9E37_79B9u) ^
                ((uint)y * 0x85EB_CA6Bu) ^
                ((uint)z * 0xC2B2_AE35u);
        }

        return values;
    }

    internal static uint[] Checkerboard(int levels)
    {
        var side = 1 << levels;
        var values = new uint[CountFor(levels)];

        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
            values[Index(side, x, y, z)] = ((x ^ y ^ z) & 1) == 0 ? 0u : uint.MaxValue;

        return values;
    }

    internal static uint[] XorShift(int levels, uint seed = 0xC0FF_EE11u)
    {
        var values = new uint[CountFor(levels)];
        var state = seed;

        for (var i = 0; i < values.Length; i++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            values[i] = state;
        }

        return values;
    }

    internal static uint[] SparseOctants(int levels)
    {
        var side = 1 << levels;
        var values = new uint[CountFor(levels)];
        var half = side / 2;

        for (var x = 0; x < half; x++)
        for (var y = 0; y < half; y++)
        for (var z = 0; z < half; z++)
            values[Index(side, x, y, z)] = 7u;

        values[^1] = 11u;
        return values;
    }

    internal static int CountFor(int levels) => 1 << (levels * 3);

    internal static int Index(int side, int x, int y, int z) => (x * side * side) + (y * side) + z;
}
