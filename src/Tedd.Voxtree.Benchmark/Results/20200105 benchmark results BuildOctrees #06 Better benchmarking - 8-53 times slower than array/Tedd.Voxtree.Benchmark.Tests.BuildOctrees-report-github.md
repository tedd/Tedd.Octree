``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
AMD Ryzen 9 3950X, 1 CPU, 32 logical and 16 physical cores
.NET Core SDK=3.1.100
  [Host]    : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT
  OutOfProc : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT

Job=OutOfProc  Jit=RyuJit  Platform=X64  
Runtime=.NET Core 3.1  Force=True  InvocationCount=1  
LaunchCount=1  UnrollFactor=1  

```
|         Method | Levels | BuildTimes |      Mean |     Error |    StdDev |       Min |        Max |    Median |       P95 |       P90 | Iterations |   Op/s | Ratio | RatioSD | Baseline |     Gen 0 |     Gen 1 | Gen 2 | Allocated | TotalIssues/Op | BranchInstructions/Op | BranchMispredictions/Op |
|--------------- |------- |----------- |----------:|----------:|----------:|----------:|-----------:|----------:|----------:|----------:|-----------:|-------:|------:|--------:|--------- |----------:|----------:|------:|----------:|---------------:|----------------------:|------------------------:|
|    BuildOctree |      6 |         10 | 96.897 ms | 1.7057 ms | 1.5955 ms | 94.769 ms | 100.001 ms | 96.422 ms | 99.498 ms | 99.116 ms |      15.00 |  10.32 | 18.32 |    1.67 |       No | 9000.0000 | 4000.0000 |     - |  88.12 MB |    207,244,052 |            80,516,542 |               1,135,957 |
| BuildOctreeDev |      6 |         10 | 87.419 ms | 1.1140 ms | 1.0420 ms | 86.214 ms |  89.588 ms | 87.062 ms | 89.498 ms | 88.952 ms |      15.00 |  11.44 | 16.54 |    1.63 |       No | 9000.0000 | 4000.0000 |     - |  88.12 MB |    203,349,261 |            79,237,228 |               1,090,291 |
|     BuildArray |      6 |         10 |  5.261 ms | 0.2194 ms | 0.6468 ms |  4.093 ms |   6.439 ms |  5.321 ms |  6.255 ms |  6.165 ms |     100.00 | 190.09 |  1.00 |    0.00 |      Yes |         - |         - |     - |     10 MB |     10,477,125 |             4,480,028 |                  68,746 |
