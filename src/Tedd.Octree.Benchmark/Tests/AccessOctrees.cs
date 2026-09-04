using BenchmarkDotNet.Attributes;
using Tedd.Octree.Benchmark.Archive.V1;
using OptimizedOctree = global::Tedd.Octree.Octree;
using OptimizedOctreeSpan = global::Tedd.Octree.OctreeSpan;

namespace Tedd.Octree.Benchmark.Tests;

[MemoryDiagnoser]
public class AccessOctrees
{
    private const int QueriesPerInvocation = 1_024;

    private uint[] _source = null!;
    private int[] _x = null!;
    private int[] _y = null!;
    private int[] _z = null!;
    private int[] _indices = null!;
    private byte[] _spanData = null!;
    private int _spanLength;
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

    [Params(QueryPattern.Sequential, QueryPattern.Random)]
    public QueryPattern Queries { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _source = BenchmarkData.Create(Levels, Pattern);
        (_x, _y, _z, _indices) = BenchmarkData.CreateQueries(Levels, Queries, QueriesPerInvocation);

        _v1 = new OctreeV1(Levels);
        _v1.Build(_source);

        _owned = new OptimizedOctree(Levels);
        _owned.Build(_source);

        _spanData = new byte[OptimizedOctree.GetRequiredSize(_source, Levels)];
        if (!OptimizedOctree.TryBuild(_source, Levels, _spanData, out _spanLength))
        {
            throw new InvalidOperationException("The size returned by GetRequiredSize was insufficient.");
        }

        ValidateImplementations();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = QueriesPerInvocation, Description = "V1 owned")]
    public uint V1_Owned()
    {
        var checksum = 0u;
        for (var i = 0; i < QueriesPerInvocation; i++)
        {
            checksum = unchecked(checksum + _v1.Get(_x[i], _y[i], _z[i]));
        }

        return checksum;
    }

    [Benchmark(OperationsPerInvoke = QueriesPerInvocation, Description = "V2 owned")]
    public uint V2_Owned()
    {
        var checksum = 0u;
        for (var i = 0; i < QueriesPerInvocation; i++)
        {
            checksum = unchecked(checksum + _owned.Get(_x[i], _y[i], _z[i]));
        }

        return checksum;
    }

    [Benchmark(OperationsPerInvoke = QueriesPerInvocation, Description = "V2 caller span")]
    public uint V2_SpanBacked()
    {
        var tree = new OptimizedOctreeSpan(_spanData.AsSpan(0, _spanLength));
        var checksum = 0u;
        for (var i = 0; i < QueriesPerInvocation; i++)
        {
            checksum = unchecked(checksum + tree.Get(_x[i], _y[i], _z[i]));
        }

        return checksum;
    }

    [Benchmark(OperationsPerInvoke = QueriesPerInvocation, Description = "Array read control")]
    public uint ArrayRead_Control()
    {
        var checksum = 0u;
        for (var i = 0; i < QueriesPerInvocation; i++)
        {
            checksum = unchecked(checksum + _source[_indices[i]]);
        }

        return checksum;
    }

    private void ValidateImplementations()
    {
        var spanTree = new OptimizedOctreeSpan(_spanData.AsSpan(0, _spanLength));
        for (var i = 0; i < QueriesPerInvocation; i++)
        {
            var expected = _source[_indices[i]];
            var v1 = _v1.Get(_x[i], _y[i], _z[i]);
            var owned = _owned.Get(_x[i], _y[i], _z[i]);
            var spanBacked = spanTree.Get(_x[i], _y[i], _z[i]);

            if (v1 != expected || owned != expected || spanBacked != expected)
            {
                throw new InvalidOperationException(
                    $"Lookup disagreement at ({_x[i]}, {_y[i]}, {_z[i]}): " +
                    $"array={expected}, v1={v1}, owned={owned}, span={spanBacked}.");
            }
        }
    }
}
