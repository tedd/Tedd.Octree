using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Tedd.Octree.Benchmark.Tests
{
    [ClrJob(), CoreJob]
    [RPlotExporter, RankColumn]

    public class BuildOctrees
    {

        private UInt32[] data;

        [Params(2, 3, 4, 5, 6, 7)]
        public int levels;

        [GlobalSetup]
        public void Setup()
        {
            var rnd = new Random();
            var chunkSize = 1 << levels;
            data = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with random data
            for (var i = 0; i < data.Length; i++)
                data[i] = (UInt32)(rnd.NextUInt32() & 0x3FFFFFFF);
        }

        [Benchmark]
        public void Build()
        {
            var octree = new Octree(levels);
            octree.Build(new Span<UInt32>(data));
        }

    }
}
