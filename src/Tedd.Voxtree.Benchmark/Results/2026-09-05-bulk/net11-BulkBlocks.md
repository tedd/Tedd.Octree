```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 34.33 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  ShortRun : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method             | Categories | Levels | Pattern | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |----------- |------- |-------- |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **LinearBuild**        | **Build**      | **5**      | **Terrain** |  **43.500 μs** |  **6.6999 μs** | **0.3672 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonBuild        | Build      | 5      | Terrain |  38.732 μs |  3.3563 μs | 0.1840 μs |  0.89 |    0.01 |         - |          NA |
| ConvertThenBuild   | Build      | 5      | Terrain |  62.078 μs |  5.4818 μs | 0.3005 μs |  1.43 |    0.01 |         - |          NA |
|                    |            |        |         |            |            |           |       |         |           |             |
| **LinearBuild**        | **Build**      | **5**      | **Dense**   | **434.627 μs** | **22.7222 μs** | **1.2455 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonBuild        | Build      | 5      | Dense   | 475.188 μs | 52.3215 μs | 2.8679 μs |  1.09 |    0.01 |         - |          NA |
| ConvertThenBuild   | Build      | 5      | Dense   | 453.876 μs |  2.1576 μs | 0.1183 μs |  1.04 |    0.00 |         - |          NA |
|                    |            |        |         |            |            |           |       |         |           |             |
| **LinearExtract**      | **Extract**    | **5**      | **Terrain** |  **55.226 μs** | **13.6398 μs** | **0.7476 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonExtract      | Extract    | 5      | Terrain |  46.370 μs |  8.2233 μs | 0.4507 μs |  0.84 |    0.01 |         - |          NA |
| PointMortonExtract | Extract    | 5      | Terrain | 480.718 μs | 56.3879 μs | 3.0908 μs |  8.71 |    0.11 |         - |          NA |
|                    |            |        |         |            |            |           |       |         |           |             |
| **LinearExtract**      | **Extract**    | **5**      | **Dense**   |   **5.432 μs** |  **0.6245 μs** | **0.0342 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonExtract      | Extract    | 5      | Dense   |  61.926 μs | 15.2219 μs | 0.8344 μs | 11.40 |    0.15 |         - |          NA |
| PointMortonExtract | Extract    | 5      | Dense   |  90.886 μs | 35.5216 μs | 1.9471 μs | 16.73 |    0.32 |         - |          NA |
