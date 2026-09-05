```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 32.53 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method        | Moving | Mean      | Error        | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------- |------- |----------:|-------------:|----------:|------:|--------:|----------:|------------:|
| **PointSupport**  | **False**  |  **73.40 ns** |   **125.773 ns** |  **6.894 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| AreaSupport   | False  | 266.08 ns |    30.617 ns |  1.678 ns |  3.65 |    0.29 |         - |          NA |
| CachedSupport | False  |  21.12 ns |     3.144 ns |  0.172 ns |  0.29 |    0.02 |         - |          NA |
|               |        |           |              |           |       |         |           |             |
| **PointSupport**  | **True**   |  **88.45 ns** |    **74.600 ns** |  **4.089 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| AreaSupport   | True   | 319.89 ns | 1,125.206 ns | 61.676 ns |  3.62 |    0.62 |         - |          NA |
| CachedSupport | True   | 809.61 ns |   159.175 ns |  8.725 ns |  9.17 |    0.39 |         - |          NA |
