using Tedd.Voxtree;
using Model = global::Tedd.Voxtree.Octree;

namespace Tedd.Voxtree.Tests;

public class SpatialQueryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AreaAndNearestQueriesMatchDenseOracle(int pattern)
    {
        const int side = 16;
        var values = CreateValues(pattern, side);
        var tree = new Model(4, values);
        var random = new Random(4711);
        var filters = new[] { VoxelFilter.Any, VoxelFilter.NonZero, VoxelFilter.EqualTo(3), VoxelFilter.NotEqualTo(7), VoxelFilter.Masked(3, 2) };
        foreach (var filter in filters)
        for (var test = 0; test < 30; test++)
        {
            var x = random.Next(side); var y = random.Next(side); var z = random.Next(side);
            var box = new VoxelBox(x, y, z, random.Next(x, side + 1), random.Next(y, side + 1), random.Next(z, side + 1));
            var expected = new List<VoxelHit>();
            var region = new List<uint>();
            for (var xx = box.MinX; xx < box.MaxX; xx++)
            for (var yy = box.MinY; yy < box.MaxY; yy++)
            for (var zz = box.MinZ; zz < box.MaxZ; zz++)
            {
                var value = values[(xx * side + yy) * side + zz];
                region.Add(value);
                if (filter.Matches(value)) expected.Add(new VoxelHit(xx, yy, zz, value));
            }
            Assert.Equal(expected.Count != 0, tree.Any(box, filter));
            Assert.Equal(expected.Count, tree.CountMatches(box, filter));
            var hits = new VoxelHit[expected.Count];
            Assert.True(tree.Query(box, filter, hits, out var written));
            Assert.Equal(expected.Count, written);
            Assert.Equal(expected.Select(Key).OrderBy(k => k), hits.Select(Key).OrderBy(k => k));
            var decoded = Enumerable.Repeat(uint.MaxValue, box.Count + 1).ToArray();
            tree.CopyRegionTo(box, decoded);
            Assert.Equal(region, decoded.Take(box.Count));
            Assert.Equal(uint.MaxValue, decoded[^1]);
            if (expected.Count > 0)
            {
                var small = new VoxelHit[expected.Count - 1];
                Assert.False(tree.Query(box, filter, small, out written));
                Assert.Equal(small.Length, written);
                Assert.Equal(small.Length, small.Select(Key).Distinct().Count());
                Assert.All(small, h => Assert.Contains(Key(h), expected.Select(Key)));
            }

            var radius = random.Next(side + 1);
            var nearest = new List<(long Distance, int X, int Y, int Z, uint Value)>();
            for (var xx = 0; xx < side; xx++)
            for (var yy = 0; yy < side; yy++)
            for (var zz = 0; zz < side; zz++)
            {
                var distance = (long)(xx - x) * (xx - x) + (long)(yy - y) * (yy - y) + (long)(zz - z) * (zz - z);
                var value = values[(xx * side + yy) * side + zz];
                if (distance <= (long)radius * radius && filter.Matches(value))
                    nearest.Add((distance, xx, yy, zz, value));
            }
            var found = tree.TryFindNearest(x, y, z, radius, filter, out var hit);
            Assert.Equal(nearest.Count != 0, found);
            if (found)
            {
                var oracle = nearest.OrderBy(n => n.Distance).ThenBy(n => n.X).ThenBy(n => n.Y).ThenBy(n => n.Z).First();
                Assert.Equal((oracle.X, oracle.Y, oracle.Z, oracle.Value), Key(hit));
            }
        }
    }

    [Fact]
    public void TinyOccupancyQueriesMatchPointReadsAcrossOctantBoundaries()
    {
        var tree = new Model(4, CreateValues(3, 16));
        var filters = new[] { VoxelFilter.Any, VoxelFilter.NonZero, VoxelFilter.EqualTo(3), VoxelFilter.Masked(3, 2) };
        foreach (var filter in filters)
        for (var x = 0; x < 15; x++)
        for (var y = 0; y < 16; y++)
        for (var z = 0; z < 15; z++)
        {
            var box = new VoxelBox(x, y, z, x + 2, y + 1, z + 2);
            var expected = filter.Matches(tree.Get(x, y, z)) || filter.Matches(tree.Get(x, y, z + 1)) ||
                filter.Matches(tree.Get(x + 1, y, z)) || filter.Matches(tree.Get(x + 1, y, z + 1));
            Assert.Equal(expected, tree.Any(box, filter));
        }
    }

    [Fact]
    public void BoxesAndFiltersHandleExtremeValues()
    {
        Assert.Throws<ArgumentException>(() => new VoxelBox(1, 0, 0, 0, 1, 1));
        var wide = new VoxelBox(int.MinValue, 0, 0, int.MaxValue, 1, 1);
        Assert.Throws<OverflowException>(() => wide.Count);
        Assert.Equal(0, new VoxelBox(int.MinValue, 0, 0, int.MaxValue, 0, 1).Count);
        var box = new VoxelBox(0, 0, 0, 1, 1, 1);
        Assert.False(box.Intersects(new VoxelBox(1, 0, 0, 2, 1, 1)));
        Assert.True(VoxelFilter.Masked(0, uint.MaxValue).Matches(uint.MaxValue));
        Assert.True(VoxelFilter.Masked(0x80000000, uint.MaxValue).Matches(0x80000000));
        Assert.False(VoxelFilter.Masked(0x80000000, uint.MaxValue).Matches(0x7fffffff));
        Assert.True(VoxelFilter.EqualTo(uint.MaxValue).Matches(uint.MaxValue));
        Assert.False(VoxelFilter.NotEqualTo(uint.MaxValue).Matches(uint.MaxValue));
    }

    [Fact]
    public void MaximumLevelUniformQueriesDoNotExpandTheVolume()
    {
        var tree = new OctreeSpan(new byte[] { 0x4f, 0x65, 1 });
        var box = new VoxelBox(0, 0, 0, 512, 512, 512);
        Assert.Equal(134217728, tree.CountMatches(box, VoxelFilter.NonZero));
        Assert.False(tree.Any(box, VoxelFilter.EqualTo(2)));
        Span<VoxelHit> hits = stackalloc VoxelHit[1];
        Assert.False(tree.Query(box, VoxelFilter.Any, hits, out var written));
        Assert.Equal(1, written);
        Assert.True(tree.TryFindNearest(511, 511, 511, int.MaxValue, VoxelFilter.NonZero, out var hit));
        Assert.Equal((511, 511, 511, 1u), Key(hit));
    }

    [Fact]
    public void NearestHandlesInclusiveRadiusTiesAndLargeRadius()
    {
        var values = new uint[512];
        values[(3 * 8 + 4) * 8 + 4] = 7;
        values[(5 * 8 + 4) * 8 + 4] = 8;
        var tree = new Model(3, values);
        Assert.False(tree.TryFindNearest(4, 4, 4, 0, VoxelFilter.NonZero, out _));
        Assert.True(tree.TryFindNearest(4, 4, 4, 1, VoxelFilter.NonZero, out var hit));
        Assert.Equal((3, 4, 4, 7u), Key(hit));
        Assert.True(tree.TryFindNearest(4, 4, 4, int.MaxValue, VoxelFilter.NonZero, out hit));
        Assert.Equal((3, 4, 4, 7u), Key(hit));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.TryFindNearest(4, 4, 4, -1, default, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.TryFindNearest(8, 4, 4, 1, default, out _));
    }

    [Fact]
    public void EmptyAndInvalidQueriesHaveExplicitBehavior()
    {
        var tree = new Model(0, new uint[] { 42 });
        var empty = new VoxelBox(1, 0, 0, 1, 1, 1);
        Assert.False(tree.Any(empty, default));
        Assert.Equal(0, tree.CountMatches(empty, default));
        Assert.True(tree.Query(empty, default, Span<VoxelHit>.Empty, out var written));
        Assert.Equal(0, written);
        tree.CopyRegionTo(empty, Span<uint>.Empty);
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Any(new VoxelBox(-1, 0, 0, 1, 1, 1), default));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CountMatches(new VoxelBox(0, 0, 0, 2, 1, 1), default));
        Assert.Throws<ArgumentException>(() => tree.CopyRegionTo(new VoxelBox(0, 0, 0, 1, 1, 1), Span<uint>.Empty));
        Assert.Throws<InvalidOperationException>(() => new Model(0).Any(default, default));
        Assert.True(tree.TryFindNearest(0, 0, 0, 0, VoxelFilter.EqualTo(42), out var hit));
        Assert.Equal(42u, hit.Value);
    }

    [Fact]
    public void CopyAndQueryRejectEncodedStorageOverlapBeforeWrites()
    {
        var encoded = new Model(3, CreateValues(2, 8)).Data.ToArray();
        var snapshot = (byte[])encoded.Clone();
        Assert.Throws<ArgumentException>(() => CopyOverlapping(encoded));
        Assert.Equal(snapshot, encoded);
        Assert.Throws<ArgumentException>(() => QueryOverlapping(encoded));
        Assert.Equal(snapshot, encoded);
    }

    private static void CopyOverlapping(byte[] encoded) =>
        new OctreeSpan(encoded).CopyRegionTo(new VoxelBox(0, 0, 0, 1, 1, 1),
            System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(encoded.AsSpan(0, 4)));

    private static void QueryOverlapping(byte[] encoded) =>
        new OctreeSpan(encoded).Query(new VoxelBox(0, 0, 0, 1, 1, 1), default,
            System.Runtime.InteropServices.MemoryMarshal.Cast<byte, VoxelHit>(encoded.AsSpan(0, 16)), out _);

    internal static uint[] CreateValues(int pattern, int side)
    {
        var values = new uint[side * side * side];
        var random = new Random(811);
        for (var x = 0; x < side; x++)
        for (var y = 0; y < side; y++)
        for (var z = 0; z < side; z++)
            values[(x * side + y) * side + z] = pattern switch
            {
                0 => 3u,
                1 => (x < side / 2 && y < side / 2) ? 3u : 0u,
                2 => (uint)random.NextInt64(0, (long)uint.MaxValue + 1),
                _ => random.Next(16) == 0 ? (uint)random.Next(1, 8) : 0
            };
        return values;
    }

    private static (int X, int Y, int Z, uint Value) Key(VoxelHit hit) => (hit.X, hit.Y, hit.Z, hit.Value);
}
