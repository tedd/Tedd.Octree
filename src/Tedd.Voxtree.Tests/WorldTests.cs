using System.Runtime.InteropServices;
using Tedd.Voxtree;

namespace Tedd.Voxtree.Tests;

public class WorldTests
{
    [Theory]
    [InlineData(5, 5)] [InlineData(10, 5)] [InlineData(20, 5)]
    public void HugeKnownEmptyWorldsRemainOneRegion(int levels, int chunkLevels)
    {
        var world = new OctreeWorld(levels, chunkLevels, 4, 0, initiallyEmpty: true);
        var bounds = new VoxelBox(0, 0, 0, world.SideLength, world.SideLength, world.SideLength);
        Assert.True(world.IsKnownEmpty(bounds)); Assert.Equal(0, world.BranchCount);
        Assert.Equal(0, world.ResidentChunkCount);
        Assert.True(world.TryCountMatches(bounds, 3, VoxelFilter.Any, out var count));
        Assert.Equal(1L << (3 * levels), count); Assert.Equal(count, world.VoxelCount);
        var regions = new OctreeWorldRegion[1];
        Assert.True(world.QueryRegions(bounds, regions, out var written)); Assert.Equal(1, written);
        Assert.Equal(levels, regions[0].Levels); Assert.Equal(OctreeRegionKind.Empty, regions[0].Kind);
        Assert.Equal(0u, world.Get(3, world.SideLength - 1, 0, 0));
    }
    [Fact]
    public void UnloadedAndKnownAirRemainDistinctAndBulkCopiesFailBeforeWrites()
    {
        var world = new OctreeWorld(10, 5, 2, 1);
        var box = new VoxelBox(0, 0, 0, 32, 32, 32);
        Assert.False(world.IsKnownEmpty(box)); Assert.False(world.TryGet(0, 0, 0, 0, out _));
        Assert.False(world.TryAny(box, 0, VoxelFilter.NonZero, out var any)); Assert.False(any);
        Assert.Throws<InvalidOperationException>(() => world.Any(box, 0, VoxelFilter.NonZero));
        var dense = Enumerable.Repeat(99u, 16).ToArray();
        Assert.Throws<InvalidOperationException>(() => world.CopyBlockTo(0, 0, 0, 1, dense));
        Assert.All(dense, v => Assert.Equal(99u, v));
        Assert.True(world.TryLoadChunk(0, 0, 0, OctreeChunk.Empty(5, 2)));
        Assert.True(world.IsKnownEmpty(box)); Assert.Equal(0, world.ResidentChunkCount);
        Assert.True(world.TryUnloadRegion(0, 0, 0, 5)); Assert.Equal(0, world.BranchCount);
        Assert.Equal(OctreeRegionKind.Unloaded, world.GetRegion(0, 0, 0).Kind);
    }
    [Fact]
    public void CapacityFailureIsAtomicAndFreedBranchesAndSlotsAreReused()
    {
        var data = new uint[512]; data[0] = 7;
        var chunk = OctreeChunk.FromDense(3, 1, data);
        var world = new OctreeWorld(10, 3, 1, 1, branchCapacity: 7, initiallyEmpty: true);
        for (var i = 0; i < 30; i++)
        {
            world.LoadChunk(i, i, i, chunk);
            var version = world.Revision;
            Assert.False(world.TryLoadChunk(i + 1, i, i, chunk));
            Assert.Equal(version, world.Revision); Assert.Equal(1, world.ResidentChunkCount);
            Assert.True(world.TryGetChunk(i, i, i, out var resident)); Assert.Same(chunk, resident);
            world.LoadChunk(i, i, i, chunk); Assert.Equal(version, world.Revision);
            Assert.True(world.TrySetEmptyRegion(0, 0, 0, 10));
            Assert.Equal(0, world.BranchCount); Assert.Equal(0, world.ResidentChunkCount);
        }
        var noBranches = new OctreeWorld(10, 3, 1, 1, branchCapacity: 0, initiallyEmpty: true);
        Assert.False(noBranches.TryLoadChunk(0, 0, 0, chunk)); Assert.Equal(0UL, noBranches.Revision);
        Assert.Equal(0, noBranches.ResidentChunkCount);
    }
    [Fact]
    public void RandomStreamingAndQueriesMatchDenseKnowledgeOracle()
    {
        const int side = 16, chunkSide = 4;
        var world = new OctreeWorld(4, 2, 2, 64);
        var a = new uint[128]; var b = new uint[128];
        for (var i = 0; i < a.Length; i++) { a[i] = (uint)(i % 11); b[i] = i < 64 ? 0 : (uint)(i % 7); }
        var chunks = new[] { OctreeChunk.FromDense(2, 2, a), OctreeChunk.FromDense(2, 2, b) };
        var states = Enumerable.Repeat(-1, 64).ToArray(); var random = new Random(813);
        for (var step = 0; step < 250; step++)
        {
            var cx = random.Next(4); var cy = random.Next(4); var cz = random.Next(4); var mode = random.Next(4);
            if (mode < 2) { world.LoadChunk(cx, cy, cz, chunks[mode]); states[(cx * 4 + cy) * 4 + cz] = mode + 1; }
            else
            {
                var regionLevels = random.Next(2, 5); var mask = (1 << regionLevels) - 1;
                var x = (cx * 4) & ~mask; var y = (cy * 4) & ~mask; var z = (cz * 4) & ~mask;
                Assert.True(mode == 2 ? world.TrySetEmptyRegion(x, y, z, regionLevels) : world.TryUnloadRegion(x, y, z, regionLevels));
                for (var xx = x / 4; xx < (x + mask + 1) / 4; xx++)
                for (var yy = y / 4; yy < (y + mask + 1) / 4; yy++)
                for (var zz = z / 4; zz < (z + mask + 1) / 4; zz++) states[(xx * 4 + yy) * 4 + zz] = mode == 2 ? 0 : -1;
            }
            var qx = random.Next(side); var qy = random.Next(side); var qz = random.Next(side);
            var box = new VoxelBox(qx, qy, qz, random.Next(qx, side + 1), random.Next(qy, side + 1), random.Next(qz, side + 1));
            for (var channel = 0; channel < 2; channel++)
            {
                long expected = 0; var unknown = false;
                for (var x = box.MinX; x < box.MaxX; x++)
                for (var y = box.MinY; y < box.MaxY; y++)
                for (var z = box.MinZ; z < box.MaxZ; z++)
                {
                    var state = states[((x / chunkSide) * 4 + y / chunkSide) * 4 + z / chunkSide];
                    Assert.Equal(state >= 0, world.TryGet(channel, x, y, z, out var value));
                    if (state < 0) { unknown = true; continue; }
                    var local = channel * 64 + (((x & 3) * 4 + (y & 3)) * 4 + (z & 3));
                    var expectedValue = state == 0 ? 0 : state == 1 ? a[local] : b[local];
                    Assert.Equal(expectedValue, value); if (value != 0) expected++;
                }
                Assert.Equal(!unknown, world.TryCountMatches(box, channel, VoxelFilter.NonZero, out var count));
                Assert.Equal(expected, count);
                Assert.Equal(expected != 0 || !unknown, world.TryAny(box, channel, VoxelFilter.NonZero, out var any));
                Assert.Equal(expected != 0, any);
                Assert.Equal(!unknown && expected == 0, world.IsKnownEmpty(box, channel));
            }
            Assert.Equal(states.Count(s => s > 0), world.ResidentChunkCount);
        }
    }
    [Theory]
    [InlineData(DenseVoxelLayout.Linear)] [InlineData(DenseVoxelLayout.Morton)]
    public void CrossChunkExtractionAndTranslatedSubworldImportsPreserveEveryChannel(DenseVoxelLayout layout)
    {
        var world = new OctreeWorld(5, 3, 3, 4, initiallyEmpty: true);
        var source = new uint[512 * 3];
        for (var i = 0; i < source.Length; i++) source[i] = (uint)(i + 1);
        var chunk = OctreeChunk.FromDense(3, 3, source);
        world.LoadChunk(0, 0, 0, chunk); world.LoadChunk(1, 1, 1, chunk); world.LoadChunk(2, 0, 1, chunk);
        var dense = new uint[4096 * 3 + 1]; dense[^1] = 123;
        world.CopyBlockTo(3, 2, 5, 4, dense, layout);
        for (var c = 0; c < 3; c++)
        for (var x = 0; x < 16; x++)
        for (var y = 0; y < 16; y++)
        for (var z = 0; z < 16; z++)
            Assert.Equal(world.Get(c, x + 3, y + 2, z + 5), dense[c * 4096 + DenseVoxel.GetIndex(x, y, z, 4, layout)]);
        Assert.Equal(123u, dense[^1]);
        var regions = new OctreeWorldRegion[world.BranchCount * 7 + 1];
        Assert.True(world.QueryRegions(new VoxelBox(0, 0, 0, 32, 32, 32), regions, out var written));
        Assert.Equal(regions.Length, written);
        Assert.False(world.QueryRegions(new VoxelBox(0, 0, 0, 32, 32, 32), new OctreeWorldRegion[regions.Length - 1], out _));
        var larger = new OctreeWorld(10, 3, 3, 4, branchCapacity: 64);
        foreach (var region in regions) Assert.True(larger.TryLoadRegion(region, 64, 96, 128));
        for (var x = 0; x < 32; x++)
        for (var y = 0; y < 32; y++)
        for (var z = 0; z < 32; z++)
        {
            Assert.True(larger.TryGet(1, x + 64, y + 96, z + 128, out var imported), $"Missing imported voxel {x},{y},{z}; stored level {larger.GetRegion(x + 64, y + 96, z + 128).Levels}.");
            Assert.Equal(world.Get(1, x, y, z), imported);
        }
        Assert.False(larger.TryGet(0, 0, 0, 0, out _));
    }
    [Fact]
    public void WorldValidatesSchemasAlignmentBoundsAndWorkspaceAliasing()
    {
        var workspace = new int[100]; var world = new OctreeWorld(5, 3, 1, workspace, new OctreeChunk?[1], 8, true);
        Assert.Throws<ArgumentException>(() => world.LoadChunk(0, 0, 0, OctreeChunk.Empty(2, 1)));
        Assert.Throws<ArgumentException>(() => world.LoadChunk(0, 0, 0, OctreeChunk.Empty(3, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.LoadChunk(-1, 0, 0, OctreeChunk.Empty(3, 1)));
        Assert.Throws<ArgumentException>(() => world.TryUnloadRegion(1, 0, 0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.TryUnloadRegion(0, 0, 0, 2));
        Assert.Throws<ArgumentException>(() => CopyToWorkspace(world, workspace));
        Assert.True(world.IsKnownEmpty(new VoxelBox(0, 0, 0, 32, 32, 32)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OctreeWorld(21, 3, 1, 0));
    }
    [Theory]
    [InlineData(0)] [InlineData(3)]
    public void WorldAtChunkDepthNeedsNoBranchesAndCanReplaceItsRoot(int levels)
    {
        var data = Enumerable.Repeat(7u, 1 << (3 * levels)).ToArray();
        var chunk = OctreeChunk.FromDense(levels, 1, data);
        var world = new OctreeWorld(levels, levels, 1, 1, branchCapacity: 0);
        world.LoadChunk(0, 0, 0, chunk);
        Assert.Equal(7u, world.Get(0, world.SideLength - 1, 0, 0));
        Assert.Equal(0, world.BranchCount); Assert.Equal(1, world.ResidentChunkCount);
        Assert.True(world.TryLoadRegion(new OctreeWorldRegion(0, 0, 0, levels, OctreeRegionKind.Empty)));
        Assert.Equal(0, world.ResidentChunkCount); Assert.Equal(0u, world.Get(0, 0, 0, 0));
        world.LoadChunk(0, 0, 0, chunk);
        Assert.True(world.TryUnloadRegion(0, 0, 0, levels));
        Assert.False(world.TryGet(0, 0, 0, 0, out _));
    }
    [Fact]
    public void UnloadingPartOfKnownAirRequiresCapacityWithoutChangingStateOnFailure()
    {
        var world = new OctreeWorld(10, 5, 1, 0, branchCapacity: 4, initiallyEmpty: true);
        Assert.False(world.TryUnloadRegion(0, 0, 0, 5));
        Assert.Equal(0UL, world.Revision); Assert.Equal(0, world.BranchCount);
        Assert.Equal(OctreeRegionKind.Empty, world.GetRegion(0, 0, 0).Kind);
        Assert.True(world.TryUnloadRegion(0, 0, 0, 6));
        Assert.Equal(4, world.BranchCount);
        Assert.True(world.TrySetEmptyRegion(0, 0, 0, 10));
        Assert.Equal(0, world.BranchCount);
    }
    [Fact]
    public void RegionDescriptorsRejectInvalidStateOrAlignment()
    {
        Assert.Throws<ArgumentException>(() => new OctreeWorldRegion(1, 0, 0, 3, OctreeRegionKind.Empty));
        Assert.Throws<ArgumentException>(() => new OctreeWorldRegion(0, 0, 0, 3, OctreeRegionKind.Loaded));
        Assert.Throws<ArgumentException>(() => new OctreeWorldRegion(0, 0, 0, 3, (OctreeRegionKind)7));
        var chunk = OctreeChunk.Empty(3, 1);
        Assert.Throws<ArgumentException>(() => new OctreeWorldRegion(0, 0, 0, 3, OctreeRegionKind.Unloaded, chunk));
        Assert.Throws<ArgumentException>(() => new OctreeWorldRegion(0, 0, 0, 4, OctreeRegionKind.Loaded, chunk));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OctreeWorldRegion(0, 0, 0, 21, OctreeRegionKind.Empty));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(4)]
    public void MortonCopiesOfClippedHomogeneousOctantsMatchPointReads(int origin)
    {
        var data = new uint[512 * 2];
        for (var c = 0; c < 2; c++)
        for (var x = 0; x < 8; x++)
        for (var y = 0; y < 8; y++)
        for (var z = 0; z < 8; z++)
            data[c * 512 + (x * 8 + y) * 8 + z] = (uint)(c + (x / 4) * 4 + (y / 4) * 2 + z / 4);
        var chunk = OctreeChunk.FromDense(3, 2, data);
        var world = new OctreeWorld(4, 3, 2, 8, initiallyEmpty: true);
        for (var x = 0; x < 2; x++)
        for (var y = 0; y < 2; y++)
        for (var z = 0; z < 2; z++) world.LoadChunk(x, y, z, chunk);
        var output = new uint[512 * 2];
        world.CopyBlockTo(origin, origin, origin, 3, output, DenseVoxelLayout.Morton);
        for (var c = 0; c < 2; c++)
        for (var x = 0; x < 8; x++)
        for (var y = 0; y < 8; y++)
        for (var z = 0; z < 8; z++)
            Assert.Equal(world.Get(c, x + origin, y + origin, z + origin),
                output[c * 512 + DenseVoxel.GetIndex(x, y, z, 3, DenseVoxelLayout.Morton)]);
    }
    private static void CopyToWorkspace(OctreeWorld world, int[] workspace) =>
        world.CopyChannelBlockTo(0, 0, 0, 0, 1, MemoryMarshal.Cast<int, uint>(workspace));
    [Fact]
    public void StreamingLookupsAndDenseExtractionAllocateZeroAfterProvisioning()
    {
        var world = new OctreeWorld(10, 3, 2, 1, initiallyEmpty: true);
        var values = new uint[1024]; values[0] = 7; var chunk = OctreeChunk.FromDense(3, 2, values);
        var output = new uint[1024]; var regions = new OctreeWorldRegion[50];
        for (var i = 0; i < 20; i++) Exercise(world, chunk, output, regions);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) Exercise(world, chunk, output, regions);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    private static void Exercise(OctreeWorld world, OctreeChunk chunk, uint[] output, OctreeWorldRegion[] regions)
    {
        world.LoadChunk(1, 2, 3, chunk);
        _ = world.Get(0, 8, 16, 24);
        _ = world.Any(new VoxelBox(8, 16, 24, 16, 24, 32), 0, VoxelFilter.NonZero);
        world.CopyBlockTo(8, 16, 24, 3, output, DenseVoxelLayout.Morton);
        _ = world.QueryRegions(new VoxelBox(0, 0, 0, 1024, 1024, 1024), regions, out _);
        _ = world.TrySetEmptyRegion(0, 0, 0, 10);
    }
}
