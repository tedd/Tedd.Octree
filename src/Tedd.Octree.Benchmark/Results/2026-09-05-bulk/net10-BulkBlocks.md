```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 34.65 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method             | Categories | Levels | Pattern | Mean       | Error       | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |----------- |------- |-------- |-----------:|------------:|-----------:|------:|--------:|----------:|------------:|
| **LinearBuild**        | **Build**      | **5**      | **Terrain** |  **46.102 μs** |   **8.3967 μs** |  **0.4603 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonBuild        | Build      | 5      | Terrain |  41.351 μs |  37.4883 μs |  2.0549 μs |  0.90 |    0.04 |         - |          NA |
| ConvertThenBuild   | Build      | 5      | Terrain |  64.420 μs |  16.6322 μs |  0.9117 μs |  1.40 |    0.02 |         - |          NA |
|                    |            |        |         |            |             |            |       |         |           |             |
| **LinearBuild**        | **Build**      | **5**      | **Dense**   | **448.063 μs** |  **60.7992 μs** |  **3.3326 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonBuild        | Build      | 5      | Dense   | 488.012 μs | 228.0901 μs | 12.5024 μs |  1.09 |    0.03 |         - |          NA |
| ConvertThenBuild   | Build      | 5      | Dense   | 470.589 μs | 139.6151 μs |  7.6528 μs |  1.05 |    0.02 |         - |          NA |
|                    |            |        |         |            |             |            |       |         |           |             |
| **LinearExtract**      | **Extract**    | **5**      | **Terrain** |  **58.475 μs** |  **12.6925 μs** |  **0.6957 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonExtract      | Extract    | 5      | Terrain |  51.748 μs |   7.5069 μs |  0.4115 μs |  0.89 |    0.01 |         - |          NA |
| PointMortonExtract | Extract    | 5      | Terrain | 514.148 μs | 102.6250 μs |  5.6252 μs |  8.79 |    0.12 |         - |          NA |
|                    |            |        |         |            |             |            |       |         |           |             |
| **LinearExtract**      | **Extract**    | **5**      | **Dense**   |   **5.693 μs** |   **0.6437 μs** |  **0.0353 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonExtract      | Extract    | 5      | Dense   |  56.991 μs |   7.1431 μs |  0.3915 μs | 10.01 |    0.08 |         - |          NA |
| PointMortonExtract | Extract    | 5      | Dense   |  95.825 μs |  17.1073 μs |  0.9377 μs | 16.83 |    0.17 |         - |          NA |
