using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows.Configs;

namespace Tedd.Octree.Benchmark.Tests
{
    //[CoreJob]

    //[MemoryDiagnoser]
    //[TailCallDiagnoser]
    ////[EtwProfiler]
    //[ConcurrencyVisualizerProfiler]
    //[NativeMemoryProfiler]
    //[ThreadingDiagnoser]
    //[HardwareCounters(
    //    HardwareCounter.BranchMispredictions,
    //    HardwareCounter.BranchInstructions)]
    //[DisassemblyDiagnoser]


    //[RPlotExporter, RankColumn]
    //[AsciiDocExporter]
    //[CsvExporter]
    //[CsvMeasurementsExporter]
    //[HtmlExporter]
    //[PlainExporter]
    [Config(typeof(TestConfig))]
    public class BuildOctrees
    {

        private UInt32[] _data;

        [Params(2, 3, 4, 5, 6)]
        public int Levels;

        private uint[] _dataSparse;
        private Octree _octreeSparse;

        [GlobalSetup]
        public void Setup()
        {
            var rnd = new Random();
            var chunkSize = 1 << Levels;
            _data = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with random data
            for (var i = 0; i < _data.Length; i++)
                _data[i] = (UInt32)(rnd.NextUInt32() & 0x3FFFFFFF);

            _dataSparse = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with random data
            for (var i = 0; i < _dataSparse.Length; i++)
                _dataSparse[i] = (UInt32)(rnd.Next(0, 3) & 0x3FFFFFFF);

            _octreeSparse = new Octree(Levels);
            _octreeSparse.Build(new Span<UInt32>(_dataSparse));

        }

        [Benchmark]
        public void BuildOctree()
        {
            var octree = new Octree(Levels);
            octree.Build(new Span<UInt32>(_data));
        }

        [Benchmark]
        public void BuildOctreeSparse()
        {
            var octree = new Octree(Levels);
            octree.Build(new Span<UInt32>(_dataSparse));
        }

        [Benchmark(Baseline = true)]
        public void BuildArray()
        {
            var chunkSize = 1 << Levels;
            _data = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with data
            for (var i = 0; i < _data.Length; i++)
                _data[i] = (UInt32)(i & 0x3FFFFFFF);
        }
    }
}
