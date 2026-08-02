using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.CoreRun;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Tedd.Octree.Benchmark
{
    public class TestConfig : ManualConfig
    {

        public TestConfig()
        {
            AddLogger(ConsoleLogger.Default);

            AddJob(Job.Default
                .WithLaunchCount(1)
                .WithGcForce(true)
                .WithId("OutOfProc")
                .WithPlatform(Platform.X64)
                .WithJit(Jit.RyuJit)
                .WithRuntime(CoreRuntime.Core31));

            AddColumn(TargetMethodColumn.Method);
            AddColumn(new BaselineColumn());
            AddColumn(BaselineRatioColumn.RatioMean);
            AddColumn(BaselineRatioColumn.RatioStdDev);
            AddColumn(StatisticColumn.StdDev, StatisticColumn.Error, StatisticColumn.Iterations, StatisticColumn.Min, StatisticColumn.Mean, StatisticColumn.Max, StatisticColumn.Median, StatisticColumn.OperationsPerSecond, StatisticColumn.P95, StatisticColumn.P90);
            AddHardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions, HardwareCounter.TotalIssues);

            AddDiagnoser(MemoryDiagnoser.Default);

            AddAnalyser(EnvironmentAnalyser.Default);
            AddExporter(RPlotExporter.Default, AsciiDocExporter.Default, CsvExporter.Default, CsvMeasurementsExporter.Default, HtmlExporter.Default, PlainExporter.Default);

        }
    }

}
