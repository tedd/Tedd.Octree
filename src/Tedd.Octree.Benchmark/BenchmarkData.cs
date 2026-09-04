namespace Tedd.Octree.Benchmark;

public enum DataPattern
{
    Uniform,
    Sparse,
    Random,
    Clustered
}

public enum QueryPattern
{
    Sequential,
    Random
}

internal static class BenchmarkData
{
    private const uint ValueMask = 0x3FFF_FFFFu;

    public static uint[] Create(int levels, DataPattern pattern)
    {
        var width = 1 << levels;
        var data = new uint[width * width * width];
        var random = new DeterministicRandom(Seed(levels, pattern));

        switch (pattern)
        {
            case DataPattern.Uniform:
                Array.Fill(data, 18u);
                break;

            case DataPattern.Sparse:
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = (random.NextUInt32() & 63u) == 0
                        ? (random.NextUInt32() & ValueMask) | 1u
                        : 0u;
                }

                break;

            case DataPattern.Clustered:
                var half = width / 2;
                var quarter = width / 4;
                FillCube(data, width, 0, 0, 0, half, 7u);
                FillCube(data, width, half, 0, half, quarter, 19u);
                FillCube(data, width, width - quarter, width - quarter, quarter, quarter, 31u);
                break;

            case DataPattern.Random:
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = random.NextUInt32() & ValueMask;
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null);
        }

        return data;
    }

    public static (int[] X, int[] Y, int[] Z, int[] Indices) CreateQueries(
        int levels,
        QueryPattern pattern,
        int count)
    {
        var width = 1 << levels;
        var cellCount = width * width * width;
        var coordinateMask = width - 1;
        var x = new int[count];
        var y = new int[count];
        var z = new int[count];
        var indices = new int[count];
        var random = new DeterministicRandom(0x9E37_79B9u ^ (uint)levels);

        for (var i = 0; i < count; i++)
        {
            var index = pattern switch
            {
                QueryPattern.Sequential => i % cellCount,
                QueryPattern.Random => (int)(random.NextUInt32() % (uint)cellCount),
                _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null)
            };

            indices[i] = index;
            x[i] = (index >> (levels * 2)) & coordinateMask;
            y[i] = (index >> levels) & coordinateMask;
            z[i] = index & coordinateMask;
        }

        return (x, y, z, indices);
    }

    private static uint Seed(int levels, DataPattern pattern) =>
        0xA341_316Cu ^ ((uint)levels * 0x9E37_79B9u) ^ ((uint)pattern * 0x85EB_CA6Bu);

    private static void FillCube(
        uint[] data,
        int width,
        int x,
        int y,
        int z,
        int size,
        uint value)
    {
        var planeStride = width * width;
        for (var offsetX = 0; offsetX < size; offsetX++)
        {
            var plane = (x + offsetX) * planeStride;
            for (var offsetY = 0; offsetY < size; offsetY++)
            {
                var start = plane + ((y + offsetY) * width) + z;
                data.AsSpan(start, size).Fill(value);
            }
        }
    }

    private struct DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? 0x6D2B_79F5u : seed;
        }

        public uint NextUInt32()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}
