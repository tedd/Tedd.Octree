```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 31.53 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  ShortRun : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method      | Moving | Pattern | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------ |------- |-------- |-----------:|------------:|----------:|------:|--------:|----------:|------------:|
| **TreeReads**   | **False**  | **Terrain** | **3,033.1 ns** | **2,110.85 ns** | **115.70 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | False  | Terrain |   549.4 ns |   637.19 ns |  34.93 ns |  0.18 |    0.01 |         - |          NA |
|             |        |         |            |             |           |       |         |           |             |
| **TreeReads**   | **False**  | **Sparse**  | **4,666.0 ns** | **1,669.11 ns** |  **91.49 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | False  | Sparse  |   628.5 ns |   434.79 ns |  23.83 ns |  0.13 |    0.00 |         - |          NA |
|             |        |         |            |             |           |       |         |           |             |
| **TreeReads**   | **False**  | **Dense**   |   **442.9 ns** |   **282.61 ns** |  **15.49 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | False  | Dense   |   449.0 ns |   240.70 ns |  13.19 ns |  1.01 |    0.04 |         - |          NA |
|             |        |         |            |             |           |       |         |           |             |
| **TreeReads**   | **True**   | **Terrain** | **2,762.5 ns** | **2,044.95 ns** | **112.09 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | True   | Terrain | 1,436.2 ns | 1,714.21 ns |  93.96 ns |  0.52 |    0.03 |         - |          NA |
|             |        |         |            |             |           |       |         |           |             |
| **TreeReads**   | **True**   | **Sparse**  | **2,453.0 ns** |   **203.90 ns** |  **11.18 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | True   | Sparse  | 1,146.8 ns |    65.14 ns |   3.57 ns |  0.47 |    0.00 |         - |          NA |
|             |        |         |            |             |           |       |         |           |             |
| **TreeReads**   | **True**   | **Dense**   |   **250.9 ns** |     **3.85 ns** |   **0.21 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | True   | Dense   |   429.7 ns |    23.64 ns |   1.30 ns |  1.71 |    0.00 |         - |          NA |
