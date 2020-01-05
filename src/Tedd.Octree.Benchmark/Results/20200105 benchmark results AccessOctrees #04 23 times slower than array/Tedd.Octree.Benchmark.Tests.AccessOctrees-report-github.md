``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
AMD Ryzen 9 3950X, 1 CPU, 32 logical and 16 physical cores
.NET Core SDK=3.1.100
  [Host]    : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT
  OutOfProc : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT

Job=OutOfProc  Jit=RyuJit  Platform=X64  
Runtime=.NET Core 3.1  Force=True  LaunchCount=1  

```
|                  Method | Levels | AccessTimes |       Mean |     Error |    StdDev |     Median |        Min |        Max |        P95 |        P90 | Iterations |      Op/s | Ratio | RatioSD | Baseline | Gen 0 | Gen 1 | Gen 2 | Allocated | TotalIssues/Op | BranchInstructions/Op | BranchMispredictions/Op |
|------------------------ |------- |------------ |-----------:|----------:|----------:|-----------:|-----------:|-----------:|-----------:|-----------:|-----------:|----------:|------:|--------:|--------- |------:|------:|------:|----------:|---------------:|----------------------:|------------------------:|
|            AccessOctree |      6 |        1000 | 100.462 us | 0.2243 us | 0.1873 us | 100.444 us | 100.277 us | 100.999 us | 100.767 us | 100.590 us |      13.00 |   9,954.0 | 53.04 |    6.29 |       No |     - |     - |     - |       1 B |        224,531 |                74,778 |                   2,239 |
|      AccessOctreeSparse |      6 |        1000 | 101.784 us | 0.0700 us | 0.0655 us | 101.757 us | 101.721 us | 101.951 us | 101.896 us | 101.858 us |      15.00 |   9,824.7 | 52.74 |    6.48 |       No |     - |     - |     - |       1 B |        212,943 |                70,902 |                   2,086 |
|    AccessOctreeMonotype |      6 |        1000 |  15.326 us | 0.0652 us | 0.0610 us |  15.286 us |  15.264 us |  15.394 us |  15.393 us |  15.392 us |      15.00 |  65,250.7 |  7.94 |    1.00 |       No |     - |     - |     - |         - |         31,945 |                 8,053 |                     123 |
|         AccessOctreeDev |      6 |        1000 | 101.890 us | 0.1579 us | 0.1477 us | 101.912 us | 101.682 us | 102.234 us | 102.101 us | 102.029 us |      15.00 |   9,814.5 | 52.80 |    6.50 |       No |     - |     - |     - |       1 B |        220,063 |                73,262 |                   2,198 |
|   AccessOctreeDevSparse |      6 |        1000 |  99.571 us | 0.1659 us | 0.1552 us |  99.532 us |  99.384 us |  99.926 us |  99.813 us |  99.762 us |      15.00 |  10,043.1 | 51.60 |    6.36 |       No |     - |     - |     - |         - |        231,739 |                77,158 |                   2,297 |
| AccessOctreeDevMonotype |      6 |        1000 |  15.318 us | 0.0467 us | 0.0436 us |  15.315 us |  15.267 us |  15.408 us |  15.391 us |  15.370 us |      15.00 |  65,282.9 |  7.94 |    0.97 |       No |     - |     - |     - |         - |         31,921 |                 8,048 |                     124 |
|             AccessArray |      6 |        1000 |   2.043 us | 0.0765 us | 0.2254 us |   2.193 us |   1.638 us |   2.215 us |   2.211 us |   2.209 us |     100.00 | 489,510.2 |  1.00 |    0.00 |      Yes |     - |     - |     - |         - |          6,691 |                 1,678 |                      26 |
