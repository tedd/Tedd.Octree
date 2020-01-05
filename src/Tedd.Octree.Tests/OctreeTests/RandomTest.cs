using System;
using Xunit;
using Xunit.Abstractions;

namespace Tedd.OctreeDev.Tests
{
    public class RandomTest
    {
        private readonly ITestOutputHelper _output;
        private Random _rnd = new Random();

        public RandomTest(ITestOutputHelper output)
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
        public void RandomCubeTest(int levels)
        {
            var chunkSize = 1 << levels;
            var data = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with random data
            for (var i = 0; i < data.Length; i++)
                data[i] = (UInt32) (_rnd.NextUInt32() & 0x3FFFFFFF);

            var octreeDev = new Octree.OctreeDev(levels);
            octreeDev.Build(new Span<UInt32>(data));

            for (var x = 0; x < chunkSize; x++)
            {
                for (var y = 0; y < chunkSize; y++)
                {
                    for (var z = 0; z < chunkSize; z++)
                    {
                        var p = x * chunkSize * chunkSize + y * chunkSize + z;
                        var result = octreeDev.Get(x, y, z);
                        //_output.WriteLine($"{data[p]} == {result}");
                        Assert.Equal(data[p], result);
                    }
                }
            }

        }
    }
}