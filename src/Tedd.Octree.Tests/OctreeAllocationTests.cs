using System.Runtime.CompilerServices;
using OctreeModel = global::Tedd.Octree.Octree;
using OctreeView = global::Tedd.Octree.OctreeSpan;

namespace Tedd.Octree.Tests;

public sealed class OctreeAllocationTests
{
    private static uint _sink;

    [Fact]
    public void TryBuild_AllocatesZeroBytesAfterWarmup()
    {
        var source = TestData.Coordinates(3);
        var destination = new byte[OctreeModel.GetRequiredSize(source, 3)];

        _sink = ExerciseTryBuild(source, destination, 2_000);
        ForceFullCollection();

        var before = GC.GetAllocatedBytesForCurrentThread();
        _sink = ExerciseTryBuild(source, destination, 500);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void OwnedGetTryGetAndContains_AllocateZeroBytesAfterWarmup()
    {
        var tree = new OctreeModel(4, TestData.XorShift(4));

        _sink = ExerciseOwnedQueries(tree, 20_000);
        ForceFullCollection();

        var before = GC.GetAllocatedBytesForCurrentThread();
        _sink = ExerciseOwnedQueries(tree, 20_000);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SpanGetTryGetAndContains_AllocateZeroBytesAfterWarmup()
    {
        var encoded = new OctreeModel(4, TestData.XorShift(4)).Data.ToArray();
        var view = new OctreeView(encoded);

        _sink = ExerciseViewQueries(view, 20_000);
        ForceFullCollection();

        var before = GC.GetAllocatedBytesForCurrentThread();
        _sink = ExerciseViewQueries(view, 20_000);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SpanViewCreation_AllocatesZeroBytesAfterWarmup()
    {
        var tree = new OctreeModel(3, TestData.Coordinates(3));
        var encoded = tree.Data.ToArray();

        _sink = ExerciseViewConstruction(encoded, 20_000);
        _sink ^= ExerciseTryCreate(encoded, 20_000);
        _sink ^= ExerciseAsSpan(tree, 20_000);
        ForceFullCollection();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = ExerciseViewConstruction(encoded, 20_000);
        checksum ^= ExerciseTryCreate(encoded, 20_000);
        checksum ^= ExerciseAsSpan(tree, 20_000);
        _sink = checksum;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ExerciseTryBuild(uint[] source, byte[] destination, int iterations)
    {
        uint checksum = 0;

        for (var i = 0; i < iterations; i++)
        {
            if (!OctreeModel.TryBuild(source, 3, destination, out var written))
                throw new InvalidOperationException("A pre-sized destination was rejected.");

            checksum += (uint)written;
            checksum ^= destination[i % destination.Length];
        }

        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ExerciseOwnedQueries(OctreeModel tree, int iterations)
    {
        uint checksum = 0;
        var mask = tree.SideLength - 1;

        for (var i = 0; i < iterations; i++)
        {
            var x = i & mask;
            var y = (i >> 3) & mask;
            var z = (i >> 6) & mask;

            checksum ^= tree.Get(x, y, z);
            if (!tree.TryGet(x, y, z, out var value))
                throw new InvalidOperationException("An in-range coordinate was rejected.");
            checksum += value;
            if (!tree.Contains(x, y, z))
                throw new InvalidOperationException("An in-range coordinate was not contained.");
        }

        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ExerciseViewQueries(OctreeView view, int iterations)
    {
        uint checksum = 0;
        var mask = view.SideLength - 1;

        for (var i = 0; i < iterations; i++)
        {
            var x = i & mask;
            var y = (i >> 3) & mask;
            var z = (i >> 6) & mask;

            checksum ^= view.Get(x, y, z);
            if (!view.TryGet(x, y, z, out var value))
                throw new InvalidOperationException("An in-range coordinate was rejected.");
            checksum += value;
            if (!view.Contains(x, y, z))
                throw new InvalidOperationException("An in-range coordinate was not contained.");
        }

        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ExerciseViewConstruction(byte[] encoded, int iterations)
    {
        uint checksum = 0;

        for (var i = 0; i < iterations; i++)
        {
            var view = new OctreeView(encoded);
            checksum += (uint)view.EncodedLength;
        }

        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ExerciseTryCreate(byte[] encoded, int iterations)
    {
        uint checksum = 0;

        for (var i = 0; i < iterations; i++)
        {
            if (!OctreeView.TryCreate(encoded, out var view))
                throw new InvalidOperationException("A valid encoding was rejected.");
            checksum += (uint)view.EncodedLength;
        }

        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ExerciseAsSpan(OctreeModel tree, int iterations)
    {
        uint checksum = 0;

        for (var i = 0; i < iterations; i++)
        {
            var view = tree.AsSpan();
            checksum += (uint)view.EncodedLength;
        }

        return checksum;
    }

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
