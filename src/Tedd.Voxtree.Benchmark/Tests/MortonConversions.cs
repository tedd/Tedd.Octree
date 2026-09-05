using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Tedd.Voxtree;
using Model = global::Tedd.Voxtree.Octree;
using Before = Tedd.Voxtree.Benchmark.Archive.MortonBefore;

namespace Tedd.Voxtree.Benchmark.Tests;

public enum MortonPattern { Uniform, LastVoxel, Runs, Dense }

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
public class MortonConversions
{
    [Params(5, 7)] public int Levels { get; set; }
    [ParamsAllValues] public MortonPattern Pattern { get; set; }
    private uint[] _source = null!, _linear = null!, _output = null!;
    private byte[] _encoded = null!;
    private Model _tree = null!;
    private Before.Octree _beforeTree = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new uint[1 << (Levels * 3)];
        var random = new Random(42);
        for (var i = 0; i < _source.Length; i++)
            _source[i] = Pattern switch
            {
                MortonPattern.Uniform => 7u,
                MortonPattern.LastVoxel => i == _source.Length - 1 ? 11u : 7u,
                // Runs cross octant boundaries; they are not all collapsible tree leaves.
                MortonPattern.Runs => (uint)((i / 1009) % 5),
                _ => (uint)random.NextInt64(0, 1L << 32)
            };
        _output = new uint[_source.Length];
        _linear = new uint[_source.Length];
        Before.DenseVoxel.Convert(_source, _linear, Levels, Before.DenseVoxelLayout.Morton, Before.DenseVoxelLayout.Linear);
        _encoded = new byte[Model.GetMaximumSize(Levels)];
        _tree = new Model(Levels);
        _tree.Build(_source, DenseVoxelLayout.Morton);
        _beforeTree = new Before.Octree(Levels);
        _beforeTree.Build(_source, Before.DenseVoxelLayout.Morton);
        if (!_tree.Data.Span.SequenceEqual(_beforeTree.Data.Span))
            throw new InvalidOperationException("Archived/current encoded bytes differ.");
        var written = BuildMorton();
        if (!_tree.Data.Span.SequenceEqual(_encoded.AsSpan(0, written)))
            throw new InvalidOperationException("Morton build parity failed.");
        ExtractMorton();
        if (!_source.AsSpan().SequenceEqual(_output))
            throw new InvalidOperationException("Morton extraction failed.");
        ExtractBefore();
        if (!_source.AsSpan().SequenceEqual(_output))
            throw new InvalidOperationException("Archived Morton extraction failed.");
        if (BuildBefore() != written || LinearBuild() != written || LinearBuildBefore() != written)
            throw new InvalidOperationException("Archived/current build lengths differ.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Build")]
    public int BuildBefore() => Before.Octree.Build(_source, Levels, _encoded, Before.DenseVoxelLayout.Morton);
    [Benchmark, BenchmarkCategory("Build")]
    public int BuildMorton() => Model.Build(_source, Levels, _encoded, DenseVoxelLayout.Morton);
    [Benchmark(Baseline = true), BenchmarkCategory("Extract")]
    public void ExtractBefore() => _beforeTree.CopyBlockTo(0, 0, 0, Levels, _output, Before.DenseVoxelLayout.Morton);
    [Benchmark, BenchmarkCategory("Extract")]
    public void ExtractMorton() => _tree.CopyBlockTo(0, 0, 0, Levels, _output, DenseVoxelLayout.Morton);
    [Benchmark(Baseline = true), BenchmarkCategory("LinearBuild")]
    public int LinearBuildBefore() => Before.Octree.Build(_linear, Levels, _encoded);
    [Benchmark, BenchmarkCategory("LinearBuild")]
    public int LinearBuild() => Model.Build(_linear, Levels, _encoded);
}

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
public class MortonLayouts
{
    [Params(5, 7)] public int Levels { get; set; }
    private uint[] _linear = null!, _morton = null!, _output = null!;

    [GlobalSetup]
    public void Setup()
    {
        _linear = new uint[1 << (Levels * 3)];
        for (var i = 0; i < _linear.Length; i++) _linear[i] = (uint)i;
        _morton = new uint[_linear.Length];
        _output = new uint[_linear.Length];
        DenseVoxel.Convert(_linear, _morton, Levels, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
        MortonToLinear();
        if (!_linear.AsSpan().SequenceEqual(_output)) throw new InvalidOperationException("Linear conversion failed.");
        LinearToMorton();
        if (!_morton.AsSpan().SequenceEqual(_output)) throw new InvalidOperationException("Morton conversion failed.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("ToLinear")]
    public void MortonToLinearBefore() =>
        Before.DenseVoxel.Convert(_morton, _output, Levels, Before.DenseVoxelLayout.Morton, Before.DenseVoxelLayout.Linear);
    [Benchmark, BenchmarkCategory("ToLinear")]
    public void MortonToLinear() =>
        DenseVoxel.Convert(_morton, _output, Levels, DenseVoxelLayout.Morton, DenseVoxelLayout.Linear);
    [Benchmark(Baseline = true), BenchmarkCategory("ToMorton")]
    public void LinearToMortonBefore() =>
        Before.DenseVoxel.Convert(_linear, _output, Levels, Before.DenseVoxelLayout.Linear, Before.DenseVoxelLayout.Morton);
    [Benchmark, BenchmarkCategory("ToMorton")]
    public void LinearToMorton() =>
        DenseVoxel.Convert(_linear, _output, Levels, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
}
