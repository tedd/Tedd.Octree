using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Tedd.Voxtree;
using Model = global::Tedd.Voxtree.Octree;

namespace Tedd.Voxtree.Benchmark.Tests;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
public class BulkBlocks
{
    [Params(3, 5)] public int Levels { get; set; }
    [Params(SpatialPattern.Terrain, SpatialPattern.Dense)] public SpatialPattern Pattern { get; set; }
    private Model _tree = null!;
    private uint[] _linear = null!, _morton = null!, _scratch = null!, _output = null!;
    private byte[] _encoded = null!;
    private int _origin;
    [GlobalSetup]
    public void Setup()
    {
        _tree = new Model(5, SpatialData.Create(Pattern)); _origin = Levels == 5 ? 0 : 7;
        _linear = new uint[1 << (Levels * 3)]; _morton = new uint[_linear.Length];
        _scratch = new uint[_linear.Length]; _output = new uint[_linear.Length];
        _tree.CopyBlockTo(_origin, _origin, _origin, Levels, _linear);
        DenseVoxel.Convert(_linear, _morton, Levels, DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
        _encoded = new byte[Model.GetMaximumSize(Levels)];
        if (LinearBuild() != MortonBuild() || MortonBuild() != ConvertThenBuild()) throw new InvalidOperationException("Bulk build parity failed.");
        MortonExtract(); if (!_morton.AsSpan().SequenceEqual(_output)) throw new InvalidOperationException("Morton extraction failed.");
        PointMortonExtract(); if (!_morton.AsSpan().SequenceEqual(_output)) throw new InvalidOperationException("Point extraction failed.");
    }
    [Benchmark(Baseline = true), BenchmarkCategory("Build")]
    public int LinearBuild() => Model.Build(_linear, Levels, _encoded);
    [Benchmark, BenchmarkCategory("Build")]
    public int MortonBuild() => Model.Build(_morton, Levels, _encoded, DenseVoxelLayout.Morton);
    [Benchmark, BenchmarkCategory("Build")]
    public int ConvertThenBuild()
    {
        DenseVoxel.Convert(_morton, _scratch, Levels, DenseVoxelLayout.Morton, DenseVoxelLayout.Linear);
        return Model.Build(_scratch, Levels, _encoded);
    }
    [Benchmark(Baseline = true), BenchmarkCategory("Extract")]
    public void LinearExtract() => _tree.CopyBlockTo(_origin, _origin, _origin, Levels, _output);
    [Benchmark, BenchmarkCategory("Extract")]
    public void MortonExtract() => _tree.CopyBlockTo(_origin, _origin, _origin, Levels, _output, DenseVoxelLayout.Morton);
    [Benchmark, BenchmarkCategory("Extract")]
    public void PointMortonExtract()
    {
        var side = 1 << Levels;
        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
            _output[global::Tedd.MortonEncoding.Encode((uint)x, (uint)y, (uint)z)] = _tree.Get(x + _origin, y + _origin, z + _origin);
    }
}

[MemoryDiagnoser]
public class BulkWorld
{
    [Params(10, 16)] public int WorldLevels { get; set; }
    [Params(3, 5)] public int ChunkLevels { get; set; }
    private OctreeWorld _world = null!, _empty = null!, _streaming = null!;
    private OctreeChunk _chunk = null!;
    private uint[] _output = null!;
    private OctreeWorldRegion[] _regions = null!;
    private int _side;
    private VoxelBox _whole;
    [GlobalSetup]
    public void Setup()
    {
        _side = 1 << ChunkLevels;
        var values = new uint[(1 << (3 * ChunkLevels)) * 4];
        for (var c = 0; c < 4; c++)
        for (var x = 0; x < _side; x++)
        for (var y = 0; y < _side; y++)
        for (var z = 0; z < _side; z++) values[c * _side * _side * _side + (x * _side + y) * _side + z] = y < _side / 2 ? (uint)(c + 1) : 0;
        _chunk = OctreeChunk.FromDense(ChunkLevels, 4, values);
        _world = new OctreeWorld(WorldLevels, ChunkLevels, 4, 8, initiallyEmpty: true);
        _empty = new OctreeWorld(WorldLevels, ChunkLevels, 4, 0, initiallyEmpty: true);
        _streaming = new OctreeWorld(WorldLevels, ChunkLevels, 4, 1, initiallyEmpty: true);
        for (var x = 0; x < 2; x++) for (var y = 0; y < 2; y++) for (var z = 0; z < 2; z++) _world.LoadChunk(x, y, z, _chunk);
        _output = new uint[values.Length]; _regions = new OctreeWorldRegion[1 + 7 * _world.BranchCount];
        _whole = new VoxelBox(0, 0, 0, _world.SideLength, _world.SideLength, _world.SideLength);
        CrossChunkLinear();
        for (var c = 0; c < 4; c++)
        for (var x = 0; x < _side; x++)
        for (var y = 0; y < _side; y++)
        for (var z = 0; z < _side; z++)
            if (_output[c * _side * _side * _side + (x * _side + y) * _side + z] != _world.Get(c, x + _side / 2, y + _side / 2, z + _side / 2))
                throw new InvalidOperationException("World extraction parity failed.");
    }
    [Benchmark] public bool EmptyWholeWorld() => _empty.IsKnownEmpty(_whole);
    [Benchmark] public uint LoadedPoint() => _world.Get(0, 1, 1, 1);
    [Benchmark] public void CrossChunkLinear() => _world.CopyBlockTo(_side / 2, _side / 2, _side / 2, ChunkLevels, _output);
    [Benchmark] public void CrossChunkMorton() => _world.CopyBlockTo(_side / 2, _side / 2, _side / 2, ChunkLevels, _output, DenseVoxelLayout.Morton);
    [Benchmark] public int EnumerateRegions() { _world.QueryRegions(_whole, _regions, out var written); return written; }
    [Benchmark] public bool LoadAndEvict()
    {
        _streaming.LoadChunk(1, 2, 3, _chunk);
        return _streaming.TrySetEmptyRegion(_side, _side * 2, _side * 3, ChunkLevels);
    }
}
