using BenchmarkDotNet.Attributes;
using Tedd.Octree.Benchmark.Archive.V1;
using OptimizedOctree = global::Tedd.Octree.Octree;

namespace Tedd.Octree.Benchmark.Tests;

[MemoryDiagnoser]
public class BuildOctrees
{
    private uint[] _source = null!;
    private uint[] _arrayDestination = null!;
    private byte[] _spanDestination = null!;
    private OctreeV1 _v1 = null!;
    private OptimizedOctree _owned = null!;

    [Params(3, 5)]
    public int Levels { get; set; }

    [Params(
        DataPattern.Uniform,
        DataPattern.Clustered,
        DataPattern.Sparse,
        DataPattern.Random)]
    public DataPattern Pattern { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _source = BenchmarkData.Create(Levels, Pattern);
        _arrayDestination = new uint[_source.Length];
        _spanDestination = new byte[OptimizedOctree.GetRequiredSize(_source, Levels)];
        _v1 = new OctreeV1(Levels);
        _owned = new OptimizedOctree(Levels);

        if (!OptimizedOctree.TryBuild(_source, Levels, _spanDestination, out _))
        {
            throw new InvalidOperationException("The size returned by GetRequiredSize was insufficient.");
        }
    }

    [Benchmark(Baseline = true, Description = "V1 owned")]
    public object V1_Owned()
    {
        _v1.Build(_source);
        return _v1;
    }

    [Benchmark(Description = "V2 owned")]
    public object V2_Owned()
    {
        _owned.Build(_source);
        return _owned;
    }

    [Benchmark(Description = "V2 caller span")]
    public int V2_SpanBacked()
    {
        OptimizedOctree.TryBuild(_source, Levels, _spanDestination, out var bytesWritten);
        return bytesWritten;
    }

    [Benchmark(Description = "Array copy control")]
    public uint ArrayCopy_Control()
    {
        _source.AsSpan().CopyTo(_arrayDestination);
        return _arrayDestination[^1];
    }
}
