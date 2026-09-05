using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Tedd.Octree;
using Model = global::Tedd.Octree.Octree;

namespace Tedd.Octree.Benchmark.Tests;

/// <summary>Compares independent channels with a packed word using the same voxel attributes.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SpatialChannels
{
    private readonly Model[] _channels = new Model[4];
    private readonly uint[][] _values = new uint[4][];
    private Model _packed = null!;
    private uint[] _packedValues = null!;
    private const int Queries = 1024;

    [GlobalSetup]
    public void Setup()
    {
        _packedValues = new uint[32 * 32 * 32];
        for (var i = 0; i < 4; i++) _values[i] = new uint[_packedValues.Length];
        var random = new Random(733);
        for (var x = 0; x < 32; x++)
        for (var y = 0; y < 32; y++)
        for (var z = 0; z < 32; z++)
        {
            var index = (x * 32 + y) * 32 + z;
            _values[0][index] = y < 12 ? 1u : 0u;
            _values[1][index] = y < 12 ? (uint)random.Next(8) : 0u;
            _values[2][index] = y >= 12 && y < 16 ? 1u : 0u;
            _values[3][index] = _values[2][index] != 0 ? (uint)random.Next(1, 16) : 0u;
            _packedValues[index] = _values[0][index] | (_values[1][index] << 16) |
                (_values[2][index] << 19) | (_values[3][index] << 23);
        }
        _packed = new Model(5, _packedValues);
        for (var i = 0; i < 4; i++) _channels[i] = new Model(5, _values[i]);
        if (PackedBlockReads() != SeparateBlockReads() || PackedAllReads() != SeparateAllReads())
            throw new InvalidOperationException("Channel benchmark oracle mismatch.");
        Console.WriteLine($"Encoded payload: packed={_packed.EncodedLength} B; separate={_channels.Sum(t => t.EncodedLength)} B.");
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Queries), BenchmarkCategory("BlockRead")]
    public uint PackedBlockReads()
    {
        var sum = 0u;
        for (var i = 0; i < Queries; i++)
            sum += _packed.Get(i & 31, (i * 17 >> 5) & 31, (i * 37 >> 3) & 31) & 0xffff;
        return sum;
    }

    [Benchmark(OperationsPerInvoke = Queries), BenchmarkCategory("BlockRead")]
    public uint SeparateBlockReads()
    {
        var sum = 0u;
        for (var i = 0; i < Queries; i++)
            sum += _channels[0].Get(i & 31, (i * 17 >> 5) & 31, (i * 37 >> 3) & 31);
        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Queries), BenchmarkCategory("AllReads")]
    public uint PackedAllReads()
    {
        var sum = 0u;
        for (var i = 0; i < Queries; i++)
            sum = unchecked(sum + _packed.Get(i & 31, (i * 17 >> 5) & 31, (i * 37 >> 3) & 31));
        return sum;
    }

    [Benchmark(OperationsPerInvoke = Queries), BenchmarkCategory("AllReads")]
    public uint SeparateAllReads()
    {
        var sum = 0u;
        for (var i = 0; i < Queries; i++)
        {
            var x = i & 31; var y = (i * 17 >> 5) & 31; var z = (i * 37 >> 3) & 31;
            sum = unchecked(sum + (_channels[0].Get(x, y, z) | (_channels[1].Get(x, y, z) << 16) |
                (_channels[2].Get(x, y, z) << 19) | (_channels[3].Get(x, y, z) << 23)));
        }
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Build")]
    public int PackedBuild()
    {
        _packed.Build(_packedValues);
        return _packed.EncodedLength;
    }

    [Benchmark, BenchmarkCategory("Build")]
    public int SeparateBuild()
    {
        var length = 0;
        for (var i = 0; i < 4; i++) { _channels[i].Build(_values[i]); length += _channels[i].EncodedLength; }
        return length;
    }
}
