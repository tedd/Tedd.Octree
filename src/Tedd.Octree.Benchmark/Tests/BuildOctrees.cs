using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;

namespace Tedd.Octree.Benchmark.Tests
{
    [CoreJob]

    [MemoryDiagnoser]
    [TailCallDiagnoser]
    //[EtwProfiler]
    [ConcurrencyVisualizerProfiler]
    [NativeMemoryProfiler]
    [ThreadingDiagnoser]


    [RPlotExporter, RankColumn]
    [AsciiDocExporter]
    [CsvExporter]
    [CsvMeasurementsExporter]
    [HtmlExporter]
    [PlainExporter]

    public class BuildOctrees
    {

        private UInt32[] data;

        [Params(2, 3, 4, 5, 6)]
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
        public void BuildOctree()
        {
            var octree = new Octree(levels);
            octree.Build(new Span<UInt32>(data));
        }

        [Benchmark]
        public void BuildArray()
        {
            var chunkSize = 1 << levels;
            data = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with data
            for (var i = 0; i < data.Length; i++)
                data[i] = (UInt32)(i & 0x3FFFFFFF);
        }
    }
}
