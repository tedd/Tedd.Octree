```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 35.58 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method        | Moving | Mean      | Error      | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------- |------- |----------:|-----------:|---------:|------:|--------:|----------:|------------:|
| **PointSupport**  | **False**  |  **57.75 ns** |  **12.403 ns** | **0.680 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| AreaSupport   | False  |  56.97 ns |   7.919 ns | 0.434 ns |  0.99 |    0.01 |         - |          NA |
| CachedSupport | False  |  19.76 ns |   3.532 ns | 0.194 ns |  0.34 |    0.00 |         - |          NA |
|               |        |           |            |          |       |         |           |             |
| **PointSupport**  | **True**   |  **76.83 ns** |   **7.927 ns** | **0.435 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| AreaSupport   | True   |  80.53 ns |   4.177 ns | 0.229 ns |  1.05 |    0.01 |         - |          NA |
| CachedSupport | True   | 806.42 ns | 176.478 ns | 9.673 ns | 10.50 |    0.12 |         - |          NA |
