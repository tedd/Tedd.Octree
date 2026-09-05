using System.Threading;

namespace Tedd.Voxtree.Tests;

public sealed class SnapshotConcurrencyTests
{
    [Fact]
    public void ConcurrentBuildsAndReadsPublishOnlyCompleteEncodings()
    {
        var first = TestData.Coordinates(3);
        var second = first.Select(value => value ^ 0x12345678u).ToArray();
        var tree = new Octree(3, first);
        using var start = new Barrier(3);
        Parallel.Invoke(
            () =>
            {
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(10)));
                for (var i = 0; i < 1000; i++) tree.Build(first);
            },
            () =>
            {
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(10)));
                for (var i = 0; i < 1000; i++) tree.Build(second, DenseVoxelLayout.Linear);
            },
            () =>
            {
                var output = new uint[first.Length];
                var retained = tree.AsSpan();
                retained.CopyTo(output);
                var expectedRetained = (uint[])output.Clone();
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(10)));
                for (var i = 0; i < 1000; i++)
                {
                    tree.CopyTo(output);
                    Assert.True(output.AsSpan().SequenceEqual(first) || output.AsSpan().SequenceEqual(second));
                    var value = tree.Get(1, 2, 3);
                    Assert.True(value == first[83] || value == second[83]);
                    retained.CopyTo(output);
                    Assert.Equal(expectedRetained, output);
                }
            });
    }
}
