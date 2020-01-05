using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Microsoft.Diagnostics.Tracing.Parsers.FrameworkEventSource;

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

        [Params(10)]
        public int BuildTimes = 1000;

        private uint[] _dataSparse;
        private uint[] _dataMonotype;

        private Octree[] _tempOctree;
        private uint[][] _tempData;
        private OctreeDev[] _tempOctreeDev;

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

            _tempOctree = new Octree[BuildTimes];
            _tempData = new uint[BuildTimes][];
            _tempOctreeDev = new OctreeDev[BuildTimes];
        }

        [IterationSetup]
        public void IterationSetup()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            for (var bt = 0; bt < BuildTimes; bt++)
            {
                _tempOctree[bt] = null;
                _tempOctreeDev[bt] = null;
                _tempData[bt] = null;
            }
        }


        [Benchmark]
        public void BuildOctree()
        {
            for (var bt = 0; bt < BuildTimes; bt++)
            {
                _tempOctree[bt] = new Octree(Levels);
                _tempOctree[bt].Build(new Span<UInt32>(_data));
            }
        }

        //[Benchmark]
        //public void BuildOctreeSparse()
        //{
        //    var octree = new Octree(Levels);
        //    octree.Build(new Span<UInt32>(_dataSparse));
        //}

        //[Benchmark]
        //public void BuildOctreeMonotype()
        //{
        //    var octree = new Octree(Levels);
        //    octree.Build(new Span<UInt32>(_dataMonotype));
        //}

        [Benchmark]
        public void BuildOctreeDev()
        {
            for (var bt = 0; bt < BuildTimes; bt++)
            {
                _tempOctreeDev[bt] = new OctreeDev(Levels);
                _tempOctreeDev[bt].Build(new Span<UInt32>(_data));
            }
        }

        //[Benchmark]
        //public void BuildOctreeDevSparse()
        //{
        //    var octree = new OctreeDev(Levels);
        //    octree.Build(new Span<UInt32>(_dataSparse));
        //}

        //[Benchmark]
        //public void BuildOctreeDevMonotype()
        //{
        //    var octree = new OctreeDev(Levels);
        //    octree.Build(new Span<UInt32>(_dataMonotype));
        //}

        [Benchmark(Baseline = true)]
        public void BuildArray()
        {
            for (var bt = 0; bt < BuildTimes; bt++)
            {
                var chunkSize = 1 << Levels;
                _tempData[bt] = new UInt32[chunkSize * chunkSize * chunkSize];
                // Fill with data
                for (var i = 0; i < _data.Length; i++)
                    _tempData[bt][i] = (UInt32)(i & 0x3FFFFFFF);
            }
        }
    }
}
