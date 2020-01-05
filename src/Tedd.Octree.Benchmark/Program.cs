using System;
using BenchmarkDotNet.Running;
using Tedd.Octree.Benchmark.Tests;

namespace Tedd.Octree.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            //var summary2 = BenchmarkRunner.Run<AccessOctrees>();

            var summary1 = BenchmarkRunner.Run<BuildOctrees>();
            
            //foreach (var levels in new[] { 1, 2, 3, 4, 5, 6, 7 })
            //{
            //    var ao = new AccessOctrees()
            //    {
            //        AccessTimes = 1000,
            //        Levels = levels
            //    };
            //    ao.Setup();
            //    ao.AccessArray();
            //}
        }
    }
}
