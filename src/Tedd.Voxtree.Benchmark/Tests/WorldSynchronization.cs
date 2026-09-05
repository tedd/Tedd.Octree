using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Tedd.Voxtree.Benchmark.Tests;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
public class WorldSynchronization
{
    private const int Operations = 1024;
    private OctreeWorld _world = null!;
    private OctreeChunk[] _chunks = null!;

    [GlobalSetup]
    public void Setup()
    {
        _chunks = new[]
        {
            OctreeChunk.FromDense(3, 1, Enumerable.Repeat(1u, 512).ToArray()),
            OctreeChunk.FromDense(3, 1, Enumerable.Repeat(2u, 512).ToArray())
        };
        _world = new OctreeWorld(10, 3, 1, 1, initiallyEmpty: true);
        _world.LoadChunk(0, 0, 0, _chunks[0]);
        if (ReadEach() != ReadBatch() || ReadEach() != RetainedChunk())
            throw new InvalidOperationException("Read benchmark parity failed.");
    }

    [GlobalCleanup] public void Cleanup() => _world.Dispose();

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations), BenchmarkCategory("Read")]
    public uint ReadEach()
    {
        uint sum = 0;
        for (var i = 0; i < Operations; i++) sum += _world.Get(0, i & 7, (i >> 3) & 7, (i >> 6) & 7);
        return sum;
    }

    [Benchmark(OperationsPerInvoke = Operations), BenchmarkCategory("Read")]
    public uint ReadBatch()
    {
        using var batch = _world.BeginReadBatch();
        uint sum = 0;
        for (var i = 0; i < Operations; i++) sum += _world.Get(0, i & 7, (i >> 3) & 7, (i >> 6) & 7);
        return sum;
    }

    // Capturing a chunk skips both locking and outer traversal on subsequent reads.
    // It intentionally keeps the old chunk if a writer replaces it later.
    [Benchmark(OperationsPerInvoke = Operations), BenchmarkCategory("Read")]
    public uint RetainedChunk()
    {
        _world.TryGetChunk(0, 0, 0, out var chunk);
        var view = chunk!.GetChannel(0);
        uint sum = 0;
        for (var i = 0; i < Operations; i++) sum += view.Get(i & 7, (i >> 3) & 7, (i >> 6) & 7);
        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations), BenchmarkCategory("Write")]
    public void WriteEach()
    {
        for (var i = 0; i < Operations; i++) _world.LoadChunk(0, 0, 0, _chunks[i & 1]);
    }

    [Benchmark(OperationsPerInvoke = Operations), BenchmarkCategory("Write")]
    public void WriteBatch()
    {
        using var batch = _world.BeginWriteBatch();
        for (var i = 0; i < Operations; i++) _world.LoadChunk(0, 0, 0, _chunks[i & 1]);
    }
}
