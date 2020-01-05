using System;
using BenchmarkDotNet.Running;
using Tedd.Octree.Benchmark.Tests;

namespace Tedd.Octree.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            //var summary1 = BenchmarkRunner.Run<BuildOctrees>();
            var summary2 = BenchmarkRunner.Run<AccessOctrees>();
        }
    }
}
