using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Tedd.Voxtree;
using Before = Tedd.Voxtree.Benchmark.Archive.MortonBefore;

namespace Tedd.Voxtree.Benchmark.Tests;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
[GenericTypeArguments(typeof(byte))]
[GenericTypeArguments(typeof(ulong))]
[GenericTypeArguments(typeof(UInt128))]
public class GenericMortonConversions<T> where T : unmanaged
{
    [Params(5)] public int Levels { get; set; }
    [ParamsAllValues] public MortonPattern Pattern { get; set; }
    private T[] _source = null!, _linear = null!, _output = null!;
    private byte[] _encoded = null!;
    private Octree<T> _tree = null!;
    private Before.Octree<T> _beforeTree = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new T[1 << (Levels * 3)];
        if (Pattern == MortonPattern.Dense)
            new Random(42).NextBytes(MemoryMarshal.AsBytes(_source.AsSpan()));
        else
            for (var i = 0; i < _source.Length; i++)
                MemoryMarshal.AsBytes(_source.AsSpan(i, 1))[0] = Pattern switch
                {
                    MortonPattern.Uniform => 7,
                    MortonPattern.LastVoxel => i == _source.Length - 1 ? (byte)11 : (byte)7,
                    _ => (byte)((i / 1009) % 5)
                };
        _output = new T[_source.Length];
        _linear = new T[_source.Length];
        Before.DenseVoxel<T>.Convert(_source, _linear, Levels, Before.DenseVoxelLayout.Morton, Before.DenseVoxelLayout.Linear);
        _encoded = new byte[Octree<T>.GetMaximumSize(Levels)];
        _tree = new Octree<T>(Levels);
        _tree.Build(_source, DenseVoxelLayout.Morton);
        _beforeTree = new Before.Octree<T>(Levels);
        _beforeTree.Build(_source, Before.DenseVoxelLayout.Morton);
        if (!_tree.Data.Span.SequenceEqual(_beforeTree.Data.Span)) throw new InvalidOperationException("Generic encoded-byte parity failed.");
        CheckBuild(BuildBefore()); CheckBuild(BuildMorton());
        CheckBuild(LinearBuildBefore()); CheckBuild(LinearBuild());
        ExtractBefore(); CheckOutput();
        ExtractMorton(); CheckOutput();
    }

    private void CheckBuild(int length)
    {
        if (!_tree.Data.Span.SequenceEqual(_encoded.AsSpan(0, length))) throw new InvalidOperationException("Generic build parity failed.");
    }
    private void CheckOutput()
    {
        if (!MemoryMarshal.AsBytes(_source.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(_output.AsSpan())))
            throw new InvalidOperationException("Generic extraction parity failed.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Build")]
    public int BuildBefore() => Before.Octree<T>.Build(_source, Levels, _encoded, Before.DenseVoxelLayout.Morton);
    [Benchmark, BenchmarkCategory("Build")]
    public int BuildMorton() => Octree<T>.Build(_source, Levels, _encoded, DenseVoxelLayout.Morton);
    [Benchmark(Baseline = true), BenchmarkCategory("Extract")]
    public void ExtractBefore() => _beforeTree.CopyBlockTo(0, 0, 0, Levels, _output, Before.DenseVoxelLayout.Morton);
    [Benchmark, BenchmarkCategory("Extract")]
    public void ExtractMorton() => _tree.CopyBlockTo(0, 0, 0, Levels, _output, DenseVoxelLayout.Morton);
    [Benchmark(Baseline = true), BenchmarkCategory("LinearBuild")]
    public int LinearBuildBefore() => Before.Octree<T>.Build(_linear, Levels, _encoded);
    [Benchmark, BenchmarkCategory("LinearBuild")]
    public int LinearBuild() => Octree<T>.Build(_linear, Levels, _encoded);
}

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
[GenericTypeArguments(typeof(byte))]
[GenericTypeArguments(typeof(ulong))]
[GenericTypeArguments(typeof(UInt128))]
public class GenericMortonLayouts<T> where T : unmanaged
{
    [Params(5)] public int Levels { get; set; }
    private T[] _linear = null!, _morton = null!, _output = null!;

    [GlobalSetup]
    public void Setup()
    {
        _linear = new T[1 << (Levels * 3)];
        new Random(42).NextBytes(MemoryMarshal.AsBytes(_linear.AsSpan()));
        _morton = new T[_linear.Length];
        _output = new T[_linear.Length];
        Before.DenseVoxel<T>.Convert(_linear, _morton, Levels, Before.DenseVoxelLayout.Linear, Before.DenseVoxelLayout.Morton);
        MortonToLinear(); Check(_linear);
        LinearToMorton(); Check(_morton);
    }
    private void Check(T[] expected)
    {
        if (!MemoryMarshal.AsBytes(expected.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(_output.AsSpan())))
            throw new InvalidOperationException("Generic layout parity failed.");
    }
    [Benchmark(Baseline = true), BenchmarkCategory("ToLinear")]
    public void MortonToLinearBefore() =>
        Before.DenseVoxel<T>.Convert(_morton, _output, Levels, Before.DenseVoxelLayout.Morton, Before.DenseVoxelLayout.Linear);
    [Benchmark, BenchmarkCategory("ToLinear")]
    public void MortonToLinear() =>
        DenseVoxel<T>.Convert(_morton, _output, Levels, DenseVoxelLayout.Morton, DenseVoxelLayout.Linear);
    [Benchmark(Baseline = true), BenchmarkCategory("ToMorton")]
    public void LinearToMortonBefore() =>
        Before.DenseVoxel<T>.Convert(_linear, _output, Levels, Before.DenseVoxelLayout.Linear, Before.DenseVoxelLayout.Morton);
    [Benchmark, BenchmarkCategory("ToMorton")]
    public void LinearToMorton() =>
        DenseVoxel<T>.Convert(_linear, _output, Levels, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
}
