using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Tedd.Voxtree.Benchmark.Tests;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
public class WorldEntityLookups
{
    private const int Operations = 1024;
    private readonly Dictionary<ChunkCoordinate, OctreeChunk> _dictionary = new();
    private WorldEntity _world = null!;
    private ChunkCoordinate[] _same = null!;
    private ChunkCoordinate[] _ten = null!;
    private ChunkCoordinate[] _misses = null!;

    [GlobalSetup]
    public void Setup()
    {
        _world = new WorldEntity(32, 1);
        for (var index = 0; index < 64; index++)
        {
            var chunk = OctreeChunk.Empty(5, 1);
            var coordinate = new ChunkCoordinate(index, -index, index * 3L);
            _dictionary.Add(coordinate, chunk);
            _world.SetChunk(coordinate, chunk);
        }
        _same = Enumerable.Repeat(new ChunkCoordinate(63, -63, 189), Operations).ToArray();
        _ten = Enumerable.Range(0, Operations)
            .Select(index => new ChunkCoordinate(63 - index % 10, -(63 - index % 10), (63 - index % 10) * 3L))
            .ToArray();
        _misses = Enumerable.Range(0, Operations)
            .Select(index => new ChunkCoordinate(1000 + index, index, -index))
            .ToArray();
        Warm(_same);
        Warm(_ten);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations), BenchmarkCategory("SameChunk")]
    public int DictionarySameChunk() => LookupDictionary(_same);

    [Benchmark(OperationsPerInvoke = Operations), BenchmarkCategory("SameChunk")]
    public int RecentCacheSameChunk() => LookupWorld(_same);

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations), BenchmarkCategory("TenChunks")]
    public int DictionaryTenChunks() => LookupDictionary(_ten);

    [Benchmark(OperationsPerInvoke = Operations), BenchmarkCategory("TenChunks")]
    public int RecentCacheTenChunks() => LookupWorld(_ten);

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations), BenchmarkCategory("Miss")]
    public int DictionaryMiss() => LookupDictionary(_misses);

    [Benchmark(OperationsPerInvoke = Operations), BenchmarkCategory("Miss")]
    public int RecentCacheMiss() => LookupWorld(_misses);

    private int LookupDictionary(ChunkCoordinate[] coordinates)
    {
        var found = 0;
        for (var index = 0; index < Operations; index++)
            if (_dictionary.TryGetValue(coordinates[index], out _)) found++;
        return found;
    }

    private int LookupWorld(ChunkCoordinate[] coordinates)
    {
        var found = 0;
        for (var index = 0; index < Operations; index++)
            if (_world.TryGetChunk(coordinates[index], out _)) found++;
        return found;
    }

    private void Warm(ChunkCoordinate[] coordinates)
    {
        for (var index = 0; index < 10; index++) _world.TryGetChunk(coordinates[index], out _);
    }
}
