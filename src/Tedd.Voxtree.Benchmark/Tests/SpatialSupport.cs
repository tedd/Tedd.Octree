using BenchmarkDotNet.Attributes;
using Tedd.Voxtree;
using Model = global::Tedd.Voxtree.Octree;

namespace Tedd.Voxtree.Benchmark.Tests;

/// <summary>A single 2-by-2 support-footprint check per entity step, including cache movement.</summary>
[MemoryDiagnoser]
public class SpatialSupport
{
    private const int Steps = 64;
    private Model _tree = null!;
    private OctreeNeighborhoodCache _cache = null!;
    private readonly VoxelBox[] _feet = new VoxelBox[Steps];

    [Params(false, true)] public bool Moving { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tree = new Model(5, SpatialData.Create(SpatialPattern.Terrain));
        _cache = new OctreeNeighborhoodCache(8);
        for (var i = 0; i < Steps; i++)
        {
            var x = Moving ? 12 + (i % 8 < 4 ? i % 4 : 3 - i % 4) : 14;
            _feet[i] = new VoxelBox(x, 14, 16, x + 2, 15, 18);
        }
        _cache.Update(_tree, 14, 15, 16);
        if (PointSupport() != AreaSupport() || PointSupport() != CachedSupport())
            throw new InvalidOperationException("Support benchmark oracle mismatch.");
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Steps)]
    public int PointSupport()
    {
        var supported = 0;
        foreach (var box in _feet)
        {
            var found = false;
            for (var x = box.MinX; x < box.MaxX && !found; x++)
            for (var z = box.MinZ; z < box.MaxZ && !found; z++)
                found = _tree.Get(x, box.MinY, z) != 0;
            if (found) supported++;
        }
        return supported;
    }

    [Benchmark(OperationsPerInvoke = Steps)]
    public int AreaSupport()
    {
        var supported = 0;
        foreach (var box in _feet)
            if (_tree.Any(box, VoxelFilter.NonZero)) supported++;
        return supported;
    }

    [Benchmark(OperationsPerInvoke = Steps)]
    public int CachedSupport()
    {
        var supported = 0;
        foreach (var box in _feet)
        {
            _cache.Update(_tree, box.MinX, 15, 16);
            if (_cache.Any(box, VoxelFilter.NonZero)) supported++;
        }
        return supported;
    }
}
