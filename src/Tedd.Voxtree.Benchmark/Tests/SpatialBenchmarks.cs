using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Tedd.Voxtree;
using Model = global::Tedd.Voxtree.Octree;

namespace Tedd.Voxtree.Benchmark.Tests;

public enum SpatialPattern { Empty, Terrain, Sparse, Dense }

internal static class SpatialData
{
    internal static uint[] Create(SpatialPattern pattern)
    {
        const int side = 32;
        var data = new uint[side * side * side];
        var random = new Random(103);
        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
            data[(x * side + y) * side + z] = pattern switch
            {
                SpatialPattern.Terrain => y < 12 + ((x + z) & 3) ? 1u : 0u,
                SpatialPattern.Sparse => random.Next(64) == 0 ? 1u : 0u,
                SpatialPattern.Dense => (uint)random.NextInt64(1, uint.MaxValue),
                _ => 0u
            };
        return data;
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SpatialSearch
{
    private Model _tree = null!;
    private readonly VoxelBox _box = new(8, 8, 8, 24, 24, 24);
    [Params(SpatialPattern.Empty, SpatialPattern.Terrain, SpatialPattern.Sparse, SpatialPattern.Dense)]
    public SpatialPattern Pattern { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tree = new Model(5, SpatialData.Create(Pattern));
        if (PointCount() != PrunedCount() || PointNearest() != PrunedNearest())
            throw new InvalidOperationException("Search benchmark oracle mismatch.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Area")]
    public int PointCount()
    {
        var result = 0;
        for (var x = _box.MinX; x < _box.MaxX; x++)
        for (var y = _box.MinY; y < _box.MaxY; y++)
        for (var z = _box.MinZ; z < _box.MaxZ; z++)
            if (_tree.Get(x, y, z) != 0) result++;
        return result;
    }

    [Benchmark, BenchmarkCategory("Area")]
    public int PrunedCount() => _tree.CountMatches(_box, VoxelFilter.NonZero);

    [Benchmark(Baseline = true), BenchmarkCategory("Nearest")]
    public int PointNearest()
    {
        var best = 65; var result = -1;
        for (var x = 8; x <= 24; x++)
        for (var y = 8; y <= 24; y++)
        for (var z = 8; z <= 24; z++)
        {
            var distance = (x - 16) * (x - 16) + (y - 16) * (y - 16) + (z - 16) * (z - 16);
            if (distance <= 64 && distance < best && _tree.Get(x, y, z) != 0)
            {
                best = distance; result = (x * 32 + y) * 32 + z;
            }
        }
        return result;
    }

    [Benchmark, BenchmarkCategory("Nearest")]
    public int PrunedNearest() => _tree.TryFindNearest(16, 16, 16, 8, VoxelFilter.NonZero, out var hit)
        ? (hit.X * 32 + hit.Y) * 32 + hit.Z : -1;
}

[MemoryDiagnoser]
public class SpatialNeighborhood
{
    private const int Frames = 64;
    private Model _tree = null!;
    private OctreeNeighborhoodCache _cache = null!;
    private readonly int[] _positions = new int[Frames];

    [Params(false, true)] public bool Moving { get; set; }
    [Params(SpatialPattern.Terrain, SpatialPattern.Sparse, SpatialPattern.Dense)]
    public SpatialPattern Pattern { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tree = new Model(5, SpatialData.Create(Pattern));
        _cache = new OctreeNeighborhoodCache(8);
        for (var i = 0; i < Frames; i++) _positions[i] = Moving ? 12 + (i % 8 < 4 ? i % 4 : 3 - i % 4) : 14;
        _cache.Update(_tree, _positions[0], 16, 16);
        if (TreeReads() != CachedReads()) throw new InvalidOperationException("Cache benchmark oracle mismatch.");
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Frames)]
    public uint TreeReads()
    {
        var sum = 0u;
        for (var frame = 0; frame < Frames; frame++)
        for (var repeat = 0; repeat < 4; repeat++)
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
            sum = unchecked(sum + _tree.Get(_positions[frame] + dx, 16 + dy, 16 + dz));
        return sum;
    }

    [Benchmark(OperationsPerInvoke = Frames)]
    public uint CachedReads()
    {
        var sum = 0u;
        for (var frame = 0; frame < Frames; frame++)
        {
            _cache.Update(_tree, _positions[frame], 16, 16);
            for (var repeat = 0; repeat < 4; repeat++)
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                sum = unchecked(sum + _cache.Get(_positions[frame] + dx, 16 + dy, 16 + dz));
        }
        return sum;
    }
}

[MemoryDiagnoser]
public class SpatialCacheMaintenance
{
    private Model _tree = null!;
    private OctreeNeighborhoodCache _cache = null!;
    private int _step;

    [GlobalSetup]
    public void Setup()
    {
        _tree = new Model(5, SpatialData.Create(SpatialPattern.Sparse));
        _cache = new OctreeNeighborhoodCache(8);
        _cache.Update(_tree, 16, 16, 16);
    }

    [Benchmark] public int Unchanged() => _cache.Update(_tree, 16, 16, 16);
    [Benchmark] public int MoveOneVoxel() => _cache.Update(_tree, 16 + ((++_step) & 1), 16, 16);
    [Benchmark] public int InvalidateAndRefill()
    {
        _cache.Invalidate();
        return _cache.Update(_tree, 16, 16, 16);
    }
}
