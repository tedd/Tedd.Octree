``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
AMD Ryzen 9 3950X, 1 CPU, 32 logical and 16 physical cores
.NET Core SDK=3.1.100
  [Host]    : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT
  OutOfProc : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT

Job=OutOfProc  Jit=RyuJit  Platform=X64  
Runtime=.NET Core 3.1  Force=True  LaunchCount=1  

```
|              Method | Levels |        Mean |     Error |    StdDev |         Min |         Max |      Median |         P95 |         P90 | Iterations |     Op/s | Ratio | RatioSD | Baseline |     Gen 0 |     Gen 1 |    Gen 2 | Allocated | TotalIssues/Op | BranchInstructions/Op | BranchMispredictions/Op |
|-------------------- |------- |------------:|----------:|----------:|------------:|------------:|------------:|------------:|------------:|-----------:|---------:|------:|--------:|--------- |----------:|----------:|---------:|----------:|---------------:|----------------------:|------------------------:|
|         BuildOctree |      6 | 22,606.5 us | 214.91 us | 167.79 us | 22,345.6 us | 22,886.5 us | 22,614.4 us | 22,826.8 us | 22,777.0 us |      12.00 |    44.24 | 93.66 |    0.76 |       No | 2062.5000 | 1156.2500 | 281.2500 |  15.67 MB |     43,319,504 |            16,803,535 |                 239,397 |
|   BuildOctreeSparse |      6 | 22,812.1 us | 537.18 us | 502.47 us | 22,218.3 us | 24,069.8 us | 22,800.5 us | 23,702.4 us | 23,371.4 us |      15.00 |    43.84 | 94.49 |    1.89 |       No | 2000.0000 | 1093.7500 | 218.7500 |  14.89 MB |     45,426,232 |            16,883,513 |                 324,477 |
| BuildOctreeMonotype |      6 |  2,643.1 us |  49.19 us |  50.52 us |  2,526.0 us |  2,739.1 us |  2,650.5 us |  2,697.7 us |  2,683.2 us |      17.00 |   378.35 | 10.94 |    0.24 |       No | 1824.2188 |   11.7188 |        - |  14.57 MB |      7,687,145 |             3,660,754 |                  41,120 |
|          BuildArray |      6 |    241.4 us |   1.41 us |   1.31 us |    237.9 us |    243.1 us |    241.6 us |    243.0 us |    242.9 us |      15.00 | 4,142.36 |  1.00 |    0.00 |      Yes |  199.9512 |  199.9512 | 199.9512 |      1 MB |        467,946 |               199,304 |                   2,303 |
