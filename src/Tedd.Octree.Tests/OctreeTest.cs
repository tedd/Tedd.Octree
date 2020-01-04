using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;
using SType = System.UInt32;

namespace Tedd.Octree.Tests
{
    public class OctreeTest
    {
        private readonly ITestOutputHelper _output;
        private Random _rnd = new Random();

        public OctreeTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void KnownCubeTest()
        {
            var cube = new SType[] {
                0x00, 0x01, 0x02, 0x03,
                0x04, 0x05, 0x06, 0x07,
                0x08, 0x09, 0x0A, 0x0B,
                0x0C, 0x0D, 0x0E, 0x0F,

                0x10, 0x11, 0x12, 0x13,
                0x14, 0x15, 0x16, 0x17,
                0x18, 0x19, 0x1A, 0x1B,
                0x1C, 0x1D, 0x1E, 0x1F,

                0x20, 0x21, 0x22, 0x23,
                0x24, 0x25, 0x26, 0x27,
                0x28, 0x29, 0x2A, 0x2B,
                0x2C, 0x2D, 0x2E, 0x2F,

                0x30, 0x31, 0x32, 0x33,
                0x34, 0x35, 0x36, 0x37,
                0x38, 0x39, 0x3A, 0x3B,
                0x3C, 0x3D, 0x3E, 0x3F
            };

         

            var compressedData = new byte[]
            {
                0, // Header
                0, // Leafnode (none terminated)
                      // All relative positions are less than 64
                00+7, // Relative jump to leaf 0 (includes rest of pointers here too)
                09+6, // Relative jump to leaf 1 (includes rest of pointers here too)
                18+5, // Relative jump to leaf 2 (includes rest of pointers here too)
                27+4, // Relative jump to leaf 3 (includes rest of pointers here too)
                36+3, // Relative jump to leaf 4 (includes rest of pointers here too)
                45+2, // Relative jump to leaf 5 (includes rest of pointers here too)
                54+1, // Relative jump to leaf 6 (includes rest of pointers here too)
                63+0, // Relative jump to leaf 7 (includes rest of pointers here too)

                255, // Leafnode 0 (all terminated)							
                0x00, 0x10, 0x04, 0x14, 0x01, 0x11, 0x05, 0x15,	// 000		// 09
                255, // Leafnode 1 (all terminated)							
                0x20, 0x30, 0x24, 0x34, 0x21, 0x31, 0x25, 0x35, // 001		// 18
                255, // Leafnode 2 (all terminated)							
                0x08, 0x18, 0x0C, 0x1C, 0x09, 0x19, 0x0D, 0x1D, // 010		// 27
                255, // Leafnode 3 (all terminated)							
                0x28, 0x38, 0x2C, 0x3C, 0x29, 0x39, 0x2D, 0x3D, // 011		// 36
                255, // Leafnode 4 (all terminated)
                0x02, 0x12, 0x06, 0x16, 0x03, 0x13, 0x07, 0x17, // 100		// 45
                255, // Leafnode 5 (all terminated)
                0x22, 0x32, 0x26, 0x36, 0x23, 0x33, 0x27, 0x37, // 101		// 54
                255, // Leafnode 6 (all terminated)
                0x0A, 0x1A, 0x0E, 0x1E, 0x0B, 0x1B, 0x0F, 0x1F, // 110		// 63
                255, // Leafnode 7 (all terminated)
                0x2A, 0x3A, 0x2E, 0x3E, 0x2B, 0x3B, 0x2F, 0x3F  // 111		// 72

                //74, // 1 byte leaf descriptor + 8x1-byte size descriptors + 8x9 byte leafs
                //255, // Leaf descriptors
                //9, // Size of leaf 0: 8 values (less than 0x3F fits into 1 byte) + 1 byte leaf descriptor
                //9, // Size of leaf 1: 8 values (less than 0x3F fits into 1 byte) + 1 byte leaf descriptor
                //9, // Size of leaf 2: 8 values (less than 0x3F fits into 1 byte) + 1 byte leaf descriptor
                //9, // Size of leaf 3: 8 values (less than 0x3F fits into 1 byte) + 1 byte leaf descriptor
                //9, // Size of leaf 4: 8 values (less than 0x3F fits into 1 byte) + 1 byte leaf descriptor
                //9, // Size of leaf 5: 8 values (less than 0x3F fits into 1 byte) + 1 byte leaf descriptor
                //9, // Size of leaf 6: 8 values (less than 0x3F fits into 1 byte) + 1 byte leaf descriptor
                //9, // Size of leaf 7: 8 values (less than 0x3F fits into 1 byte) + 1 byte leaf descriptor
                //0, // Leaf descriptors
                //0x00, 0x10, 0x04, 0x14, 0x01, 0x11, 0x05, 0x15, // 000
                //0, // Leaf descriptors
                //0x20, 0x30, 0x24, 0x34, 0x21, 0x31, 0x25, 0x35, // 001
                //0, // Leaf descriptors
                //0x08, 0x18, 0x0C, 0x1C, 0x09, 0x19, 0x0D, 0x1D, // 010
                //0, // Leaf descriptors
                //0x28, 0x38, 0x2C, 0x3C, 0x29, 0x39, 0x2D, 0x3D, // 011
                //0, // Leaf descriptors
                //0x02, 0x12, 0x06, 0x16, 0x03, 0x13, 0x07, 0x17, // 100
                //0, // Leaf descriptors
                //0x22, 0x32, 0x26, 0x36, 0x23, 0x33, 0x27, 0x37, // 101
                //0, // Leaf descriptors
                //0x0A, 0x1A, 0x0E, 0x1E, 0x0B, 0x1B, 0x0F, 0x1F, // 110
                //0, // Leaf descriptors
                //0x2A, 0x3A, 0x2E, 0x3E, 0x2B, 0x3B, 0x2F, 0x3F // 111
            };

            var octree = new Octree(2);
            octree.Build(new Span<SType>(cube));

            Assert.Equal(compressedData.ToArray(), octree.Data.ToArray());

        }

        [Fact]
        public void CompactKnownCubeTest()
        {
            var cube = new SType[32 * 32 * 32];
            Array.Fill<SType>(cube, 18);

            var octree = new Octree(5);
            octree.Build(new Span<UInt32>(cube));

            var compressedData = new byte[]
            {
                0 | 1,  // Header with bit 1 set to indicate we terminate immediately (monotype)
                18      // Type
            };

            Assert.Equal(compressedData.ToArray(), octree.Data.ToArray());
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
        public void LevelLinearTest(int levels)
        {
            var chunkSize = 1 << levels;
            var data = new UInt32[chunkSize * chunkSize * chunkSize];

            for (var i = 0; i < data.Length; i++)
                data[i] = (UInt32)(i);

            var octree = new Octree(levels);
            octree.Build(new Span<UInt32>(data));

            for (var x = 0; x < chunkSize; x++)
            {
                for (var y = 0; y < chunkSize; y++)
                {
                    for (var z = 0; z < chunkSize; z++)
                    {
                        var p = x * chunkSize * chunkSize + y * chunkSize + z;
                        _output.WriteLine($"X:{x},Y:{y},Z:{z} - {data[p]}");
                        var result = octree.Get(x, y, z);
                        _output.WriteLine($"X:{x},Y:{y},Z:{z} - {data[p]} == {result}");
                        Assert.Equal(data[p], result);
                    }
                }
            }
        }

        //[Fact]
        //public void LinearTest()
        //{
        //    int levels = 5;
        //    var chunkSize = 1 << levels;
        //    var data = new UInt16[chunkSize * chunkSize * chunkSize];


        //    for (var i = 0; i < data.Length; i++)
        //        data[i] = (UInt16)(i);

        //    var octree = new Octree15(levels);
        //    octree.Build(new Span<UInt16>(data));

        //    for (var x = 0; x < chunkSize; x++)
        //    {
        //        for (var y = 0; y < chunkSize; y++)
        //        {
        //            for (var z = 0; z < chunkSize; z++)
        //            {
        //                var p = x * chunkSize * chunkSize + y * chunkSize + z;
        //                var result = octree.Get(x, y, z);
        //                _output.WriteLine($"{data[p]} == {result}");
        //                Assert.Equal(data[p], result);
        //            }
        //        }
        //    }
        //}

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
        public void RandomTest(int levels)
        {
            var chunkSize = 1 << levels;
            var data = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with random data
            for (var i = 0; i < data.Length; i++)
                data[i] = (UInt32)(_rnd.Next() & 0x7FFF);

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
                        _output.WriteLine($"{data[p]} == {result}");
                        Assert.Equal(data[p], result);
                    }
                }
            }

        }
    }
}
