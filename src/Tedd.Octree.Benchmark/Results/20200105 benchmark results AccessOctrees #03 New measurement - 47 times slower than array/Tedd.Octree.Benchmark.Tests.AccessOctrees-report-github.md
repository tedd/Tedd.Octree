``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
AMD Ryzen 9 3950X, 1 CPU, 32 logical and 16 physical cores
.NET Core SDK=3.1.100
  [Host]    : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT
  OutOfProc : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT

Job=OutOfProc  Jit=RyuJit  Platform=X64  
Runtime=.NET Core 3.1  Force=True  LaunchCount=1  

```
|               Method | Levels | AccessTimes |       Mean |     Error |    StdDev |        Min |        Max |     Median |        P95 |        P90 | Iterations |      Op/s | Ratio | RatioSD | Baseline | Gen 0 | Gen 1 | Gen 2 | Allocated | TotalIssues/Op | BranchInstructions/Op | BranchMispredictions/Op |
|--------------------- |------- |------------ |-----------:|----------:|----------:|-----------:|-----------:|-----------:|-----------:|-----------:|-----------:|----------:|------:|--------:|--------- |------:|------:|------:|----------:|---------------:|----------------------:|------------------------:|
|         AccessOctree |      6 |        1000 | 100.542 us | 0.4694 us | 0.4390 us | 100.064 us | 101.363 us | 100.323 us | 101.275 us | 101.149 us |      15.00 |   9,946.0 | 48.07 |    2.45 |       No |     - |     - |     - |       1 B |        242,878 |                80,944 |                   2,468 |
|   AccessOctreeSparse |      6 |        1000 |  98.260 us | 0.3739 us | 0.3498 us |  97.914 us |  98.830 us |  98.196 us |  98.812 us |  98.794 us |      15.00 |  10,177.0 | 46.98 |    2.45 |       No |     - |     - |     - |         - |        218,646 |                72,860 |                   2,200 |
| AccessOctreeMonotype |      6 |        1000 |  98.623 us | 0.0830 us | 0.0648 us |  98.486 us |  98.700 us |  98.650 us |  98.683 us |  98.668 us |      12.00 |  10,139.6 | 47.26 |    2.64 |       No |     - |     - |     - |       1 B |        218,481 |                72,820 |                   2,211 |
|          AccessArray |      6 |        1000 |   2.119 us | 0.0423 us | 0.0865 us |   1.873 us |   2.226 us |   2.134 us |   2.207 us |   2.203 us |      51.00 | 471,866.0 |  1.00 |    0.00 |      Yes |     - |     - |     - |         - |          6,230 |                 1,579 |                      26 |
