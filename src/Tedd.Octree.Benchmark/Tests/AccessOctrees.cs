using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Tedd.SpanUtils;

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

    //[AllStatisticsColumn]

    //[RPlotExporter, RankColumn]
    //[AsciiDocExporter]
    //[CsvExporter]
    //[CsvMeasurementsExporter]
    //[HtmlExporter]
    //[PlainExporter]
    [Config(typeof(TestConfig))]
    public class AccessOctrees
    {

        private UInt32[] _data;
        private uint[] _dataSparse;
        private uint[] _dataMonotype;
        private Octree _octree;
        private Octree _octreeSparse;
        private Octree _octreeMonotype;

        private int _accessPos = 0;
        private int _levelMask;
        public UInt32 DebugSum;

        [Params( 6)]//2, 3, 4, 5,
        public int Levels;

        [Params(1_000)]
        public int AccessTimes = 1000;

        

        [GlobalSetup]
        public void Setup()
        {
            _levelMask = ((~0) << (32 - Levels) >> (32 - Levels));

            var rnd = new Random();
            var chunkSize = 1 << Levels;

            {
                _data = new UInt32[chunkSize * chunkSize * chunkSize];
                // Fill with random data
                for (var i = 0; i < _data.Length; i++)
                    _data[i] = (UInt32) (rnd.NextUInt32() & 0x3FFFFFFF);
             
                _octree = new Octree(Levels);
                _octree.Build(new Span<UInt32>(_data));
            }

            {
                _dataSparse = new UInt32[chunkSize * chunkSize * chunkSize];
                // Fill with random data
                for (var i = 0; i < _dataSparse.Length; i++)
                    _dataSparse[i] = (UInt32) (rnd.Next(0, 2) & 0x3FFFFFFF);

                _octreeSparse = new Octree(Levels);
                _octreeSparse.Build(new Span<UInt32>(_dataSparse));
            }

            {
                _dataMonotype = new UInt32[chunkSize * chunkSize * chunkSize];
                Array.Fill(_dataMonotype, (UInt32)18);

                _octreeMonotype = new Octree(Levels);
                _octreeMonotype.Build(new Span<UInt32>(_dataSparse));

                if (_octreeMonotype.Data.Length > 10)
                    throw new Exception("Monotype too big!");

            }

        }



        [Benchmark]
        public void AccessOctree()
        {
            for (var ac = 0; ac < AccessTimes; ac++)
            {
                _accessPos++;
                if (_accessPos >= _data.Length)
                    _accessPos = 0;

                var x = (_accessPos >> (Levels + Levels)) & _levelMask;
                var y = (_accessPos >> Levels) & _levelMask;
                var z = (_accessPos) & _levelMask;

                DebugSum = (UInt32)((x << (Levels + Levels))
                                  | (y << Levels)
                                  | (z));

                DebugSum = _octree.Get(x, y, z);
            }
        }

        [Benchmark]
        public void AccessOctreeSparse()
        {
            for (var ac = 0; ac < AccessTimes; ac++)
            {
                _accessPos++;
                if (_accessPos >= _data.Length)
                    _accessPos = 0;

                var x = (_accessPos >> (Levels + Levels)) & _levelMask;
                var y = (_accessPos >> Levels) & _levelMask;
                var z = (_accessPos) & _levelMask;

                DebugSum = (UInt32)((x << (Levels + Levels))
                                    | (y << Levels)
                                    | (z));

                DebugSum = _octreeSparse.Get(x, y, z);
            }
        }

 [Benchmark]
        public void AccessOctreeMonotype()
        {
            for (var ac = 0; ac < AccessTimes; ac++)
            {
                _accessPos++;
                if (_accessPos >= _data.Length)
                    _accessPos = 0;

                var x = (_accessPos >> (Levels + Levels)) & _levelMask;
                var y = (_accessPos >> Levels) & _levelMask;
                var z = (_accessPos) & _levelMask;

                DebugSum = (UInt32)((x << (Levels + Levels))
                                    | (y << Levels)
                                    | (z));

                DebugSum = _octreeMonotype.Get(x, y, z);
            }
        }


        [Benchmark(Baseline = true)]
        public void AccessArray()
        {
            for (var ac = 0; ac < AccessTimes; ac++)
            {
                _accessPos++;
                if (_accessPos >= _data.Length)
                    _accessPos = 0;

                var x = (_accessPos >> (Levels + Levels)) & _levelMask;
                var y = (_accessPos >> Levels) & _levelMask;
                var z = (_accessPos) & _levelMask;

                DebugSum = (UInt32)((x << (Levels + Levels))
                         | (y << Levels)
                         | (z));
                DebugSum = _data[_accessPos];
            }
        }

    }
}