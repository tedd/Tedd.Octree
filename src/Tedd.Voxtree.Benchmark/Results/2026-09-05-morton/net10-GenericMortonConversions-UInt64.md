```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 29.31 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-ZNVNAZ : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

IterationCount=5  IterationTime=100ms  LaunchCount=1  
WarmupCount=3  

```
| Method            | Categories  | Levels | Pattern   | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------ |------------ |------- |---------- |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **BuildBefore**       | **Build**       | **5**      | **Uniform**   |   **6.857 μs** |  **0.3747 μs** | **0.0973 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Uniform   |   2.641 μs |  0.1940 μs | 0.0504 μs |  0.39 |    0.01 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **BuildBefore**       | **Build**       | **5**      | **LastVoxel** |  **15.488 μs** |  **0.4404 μs** | **0.1144 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | LastVoxel |   2.790 μs |  0.0650 μs | 0.0101 μs |  0.18 |    0.00 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **BuildBefore**       | **Build**       | **5**      | **Runs**      |  **31.613 μs** |  **0.5054 μs** | **0.0782 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Runs      |   8.070 μs |  0.3612 μs | 0.0938 μs |  0.26 |    0.00 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **BuildBefore**       | **Build**       | **5**      | **Dense**     | **746.641 μs** | **10.0218 μs** | **2.6026 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Dense     | 561.112 μs | 25.6449 μs | 6.6599 μs |  0.75 |    0.01 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Uniform**   |   **2.376 μs** |  **0.0462 μs** | **0.0071 μs** |  **1.00** |    **0.00** |      **80 B** |        **1.00** |
| ExtractMorton     | Extract     | 5      | Uniform   |   2.327 μs |  0.0703 μs | 0.0109 μs |  0.98 |    0.00 |         - |        0.00 |
|                   |             |        |           |            |            |           |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **LastVoxel** |   **3.059 μs** |  **0.0654 μs** | **0.0170 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | LastVoxel |   2.591 μs |  0.0410 μs | 0.0106 μs |  0.85 |    0.01 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Runs**      |  **12.957 μs** |  **0.4960 μs** | **0.1288 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | Runs      |   6.697 μs |  0.1879 μs | 0.0291 μs |  0.52 |    0.01 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Dense**     |  **67.331 μs** |  **1.8104 μs** | **0.4701 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | Dense     |  21.272 μs |  2.5604 μs | 0.6649 μs |  0.32 |    0.01 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Uniform**   |   **6.893 μs** |  **0.2153 μs** | **0.0559 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Uniform   |   6.805 μs |  0.3145 μs | 0.0817 μs |  0.99 |    0.01 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **LastVoxel** |  **18.372 μs** |  **0.9050 μs** | **0.2350 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | LastVoxel |  18.377 μs |  1.0695 μs | 0.2778 μs |  1.00 |    0.02 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Runs**      |  **29.511 μs** |  **1.0990 μs** | **0.1701 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Runs      |  29.451 μs |  1.0521 μs | 0.2732 μs |  1.00 |    0.01 |         - |          NA |
|                   |             |        |           |            |            |           |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Dense**     | **713.425 μs** | **16.5247 μs** | **4.2914 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Dense     | 695.169 μs | 19.0814 μs | 4.9554 μs |  0.97 |    0.01 |         - |          NA |
