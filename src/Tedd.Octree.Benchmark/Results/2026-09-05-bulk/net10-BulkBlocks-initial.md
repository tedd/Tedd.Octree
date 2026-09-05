```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 34.57 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method             | Categories | Levels | Pattern | Mean         | Error         | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |----------- |------- |-------- |-------------:|--------------:|------------:|------:|--------:|----------:|------------:|
| **LinearBuild**        | **Build**      | **3**      | **Terrain** |   **2,706.3 ns** |     **112.72 ns** |     **6.18 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonBuild        | Build      | 3      | Terrain |   2,610.8 ns |   2,095.08 ns |   114.84 ns |  0.96 |    0.04 |         - |          NA |
| ConvertThenBuild   | Build      | 3      | Terrain |   3,027.7 ns |     429.36 ns |    23.53 ns |  1.12 |    0.01 |         - |          NA |
|                    |            |        |         |              |               |             |       |         |           |             |
| **LinearBuild**        | **Build**      | **3**      | **Dense**   |   **7,102.6 ns** |   **1,403.14 ns** |    **76.91 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonBuild        | Build      | 3      | Dense   |   7,567.3 ns |     595.62 ns |    32.65 ns |  1.07 |    0.01 |         - |          NA |
| ConvertThenBuild   | Build      | 3      | Dense   |   7,459.7 ns |     774.18 ns |    42.44 ns |  1.05 |    0.01 |         - |          NA |
|                    |            |        |         |              |               |             |       |         |           |             |
| **LinearBuild**        | **Build**      | **5**      | **Terrain** |  **46,987.5 ns** |  **14,270.28 ns** |   **782.20 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonBuild        | Build      | 5      | Terrain |  40,778.9 ns |  10,516.87 ns |   576.46 ns |  0.87 |    0.02 |         - |          NA |
| ConvertThenBuild   | Build      | 5      | Terrain |  69,591.5 ns |  16,972.62 ns |   930.33 ns |  1.48 |    0.03 |         - |          NA |
|                    |            |        |         |              |               |             |       |         |           |             |
| **LinearBuild**        | **Build**      | **5**      | **Dense**   | **453,504.7 ns** |  **43,184.67 ns** | **2,367.10 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonBuild        | Build      | 5      | Dense   | 477,864.9 ns | 133,925.58 ns | 7,340.91 ns |  1.05 |    0.01 |         - |          NA |
| ConvertThenBuild   | Build      | 5      | Dense   | 466,474.5 ns |  81,476.62 ns | 4,466.01 ns |  1.03 |    0.01 |         - |          NA |
|                    |            |        |         |              |               |             |       |         |           |             |
| **LinearExtract**      | **Extract**    | **3**      | **Terrain** |   **4,596.5 ns** |     **434.19 ns** |    **23.80 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonExtract      | Extract    | 3      | Terrain |   4,745.9 ns |     878.52 ns |    48.15 ns |  1.03 |    0.01 |         - |          NA |
| PointMortonExtract | Extract    | 3      | Terrain |  11,253.7 ns |   2,861.26 ns |   156.84 ns |  2.45 |    0.03 |         - |          NA |
|                    |            |        |         |              |               |             |       |         |           |             |
| **LinearExtract**      | **Extract**    | **3**      | **Dense**   |     **295.4 ns** |      **65.46 ns** |     **3.59 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonExtract      | Extract    | 3      | Dense   |     730.9 ns |      65.31 ns |     3.58 ns |  2.47 |    0.03 |         - |          NA |
| PointMortonExtract | Extract    | 3      | Dense   |   1,437.7 ns |     176.35 ns |     9.67 ns |  4.87 |    0.06 |         - |          NA |
|                    |            |        |         |              |               |             |       |         |           |             |
| **LinearExtract**      | **Extract**    | **5**      | **Terrain** |  **59,796.7 ns** |  **15,512.56 ns** |   **850.30 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonExtract      | Extract    | 5      | Terrain |  90,877.3 ns |   5,632.37 ns |   308.73 ns |  1.52 |    0.02 |         - |          NA |
| PointMortonExtract | Extract    | 5      | Terrain | 493,218.2 ns | 119,156.41 ns | 6,531.36 ns |  8.25 |    0.14 |         - |          NA |
|                    |            |        |         |              |               |             |       |         |           |             |
| **LinearExtract**      | **Extract**    | **5**      | **Dense**   |   **5,810.1 ns** |   **3,205.44 ns** |   **175.70 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonExtract      | Extract    | 5      | Dense   |  49,396.8 ns |     973.56 ns |    53.36 ns |  8.51 |    0.22 |         - |          NA |
| PointMortonExtract | Extract    | 5      | Dense   |  96,079.2 ns |  15,061.06 ns |   825.55 ns | 16.55 |    0.45 |         - |          NA |
