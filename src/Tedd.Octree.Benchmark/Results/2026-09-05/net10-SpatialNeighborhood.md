```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 38.41 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method      | Moving | Pattern | Mean       | Error        | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------ |------- |-------- |-----------:|-------------:|----------:|------:|--------:|----------:|------------:|
| **TreeReads**   | **False**  | **Terrain** | **1,628.1 ns** |      **7.01 ns** |   **0.38 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | False  | Terrain |   342.8 ns |     56.47 ns |   3.10 ns |  0.21 |    0.00 |         - |          NA |
|             |        |         |            |              |           |       |         |           |             |
| **TreeReads**   | **False**  | **Sparse**  | **2,523.9 ns** |    **243.86 ns** |  **13.37 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | False  | Sparse  |   338.3 ns |     75.69 ns |   4.15 ns |  0.13 |    0.00 |         - |          NA |
|             |        |         |            |              |           |       |         |           |             |
| **TreeReads**   | **False**  | **Dense**   |   **262.1 ns** |      **9.05 ns** |   **0.50 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | False  | Dense   |   341.3 ns |      2.99 ns |   0.16 ns |  1.30 |    0.00 |         - |          NA |
|             |        |         |            |              |           |       |         |           |             |
| **TreeReads**   | **True**   | **Terrain** | **2,006.1 ns** |  **7,210.19 ns** | **395.21 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | True   | Terrain | 1,444.1 ns |  2,910.84 ns | 159.55 ns |  0.74 |    0.13 |         - |          NA |
|             |        |         |            |              |           |       |         |           |             |
| **TreeReads**   | **True**   | **Sparse**  | **4,115.5 ns** | **12,494.27 ns** | **684.85 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | True   | Sparse  | 1,256.0 ns |    299.03 ns |  16.39 ns |  0.31 |    0.05 |         - |          NA |
|             |        |         |            |              |           |       |         |           |             |
| **TreeReads**   | **True**   | **Dense**   |   **293.9 ns** |    **194.12 ns** |  **10.64 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedReads | True   | Dense   |   480.4 ns |    231.98 ns |  12.72 ns |  1.64 |    0.06 |         - |          NA |
