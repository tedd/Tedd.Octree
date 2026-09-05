using BenchmarkDotNet.Attributes;
using Tedd.Octree;

namespace Tedd.Octree.Benchmark.Tests;

[MemoryDiagnoser]
public class BulkUniform
{
    [Params(3, 5)] public int Levels { get; set; }
    [Params(DenseVoxelLayout.Linear, DenseVoxelLayout.Morton)] public DenseVoxelLayout Layout { get; set; }
    private OctreeChunk _chunk = null!;
    private uint[] _output = null!;
    [GlobalSetup]
    public void Setup()
    {
        _chunk = OctreeChunk.Empty(Levels, 4);
        _output = new uint[(1 << (3 * Levels)) * 4];
    }
    [Benchmark]
    public void ExtractAllChannels() => _chunk.CopyBlockTo(0, 0, 0, Levels, _output, Layout);
}
