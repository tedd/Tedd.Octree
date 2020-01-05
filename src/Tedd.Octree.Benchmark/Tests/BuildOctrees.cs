using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        [Params(6)]//2, 3, 4, 5,
        public int Levels;

        private uint[] _dataSparse;
        private uint[] _dataMonotype;

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
                _dataSparse[i] = (UInt32)(rnd.Next(0, 2) & 0x3FFFFFFF);

            _dataMonotype = new UInt32[chunkSize * chunkSize * chunkSize];
            Array.Fill(_dataMonotype, (UInt32)18);

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

        [Benchmark]
        public void BuildOctreeMonotype()
        {
            var octree = new Octree(Levels);
            octree.Build(new Span<UInt32>(_dataMonotype));
        }

        [Benchmark]
        public void BuildOctreeDev()
        {
            var octree = new OctreeDev(Levels);
            octree.Build(new Span<UInt32>(_data));
        }

        [Benchmark]
        public void BuildOctreeDevSparse()
        {
            var octree = new OctreeDev(Levels);
            octree.Build(new Span<UInt32>(_dataSparse));
        }

        [Benchmark]
        public void BuildOctreeDevMonotype()
        {
            var octree = new OctreeDev(Levels);
            octree.Build(new Span<UInt32>(_dataMonotype));
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
