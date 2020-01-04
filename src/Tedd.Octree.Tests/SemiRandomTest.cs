using System;
using Xunit;
using Xunit.Abstractions;

namespace Tedd.Octree.Tests
{
    public class SemiRandomTest
    {
        private readonly ITestOutputHelper _output;
        private Random _rnd = new Random();

        public SemiRandomTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        //[InlineData(8)]
        //[InlineData(9)]
        //[InlineData(10)]
        public void SemiRandomCubeTest(int levels)
        {
            var chunkSize = 1 << levels;
            var data = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with random data
            for (var i = 0; i < data.Length; i++)
                data[i] = (UInt32)(_rnd.Next(0,4));

            var octree = new Octree(levels);
            octree.Build(new Span<UInt32>(data));

            for (var x = 0; x < chunkSize; x++)
            {
                for (var y = 0; y < chunkSize; y++)
                {
                    for (var z = 0; z < chunkSize; z++)
                    {
                        var p = x * chunkSize * chunkSize + y * chunkSize + z;
                        var result = octree.Get(x, y, z);
                        //_output.WriteLine($"{data[p]} == {result}");
                        Assert.Equal(data[p], result);
                    }
                }
            }

        }
    }
}