using System;
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
        private Octree _octree;
        private Octree _octreeSparse;

        private int _accessPos = 0;
        private int _levelMask;
        public UInt32 DebugSum;

        [Params(2, 3, 4, 5, 6)]
        public int Levels;

        [Params(1_000)]
        public int AccessTimes = 1000;



        [GlobalSetup]
        public void Setup()
        {
            _levelMask = ((~0) << (32 - Levels) >> (32 - Levels));

            var rnd = new Random();
            var chunkSize = 1 << Levels;
            _data = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with random data
            for (var i = 0; i < _data.Length; i++)
                _data[i] = (UInt32)(rnd.NextUInt32() & 0x3FFFFFFF);


            _octree = new Octree(Levels);
            _octree.Build(new Span<UInt32>(_data));

            _dataSparse = new UInt32[chunkSize * chunkSize * chunkSize];
            // Fill with random data
            for (var i = 0; i < _dataSparse.Length; i++)
                _dataSparse[i] = (UInt32)(rnd.Next(0, 3) & 0x3FFFFFFF);

            _octreeSparse = new Octree(Levels);
            _octreeSparse.Build(new Span<UInt32>(_dataSparse));


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