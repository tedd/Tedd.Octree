```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 30.12 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-ZNVNAZ : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

IterationCount=5  IterationTime=100ms  LaunchCount=1  
WarmupCount=3  

```
| Method            | Categories  | Levels | Pattern   | Mean          | Error         | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------ |------------ |------- |---------- |--------------:|--------------:|------------:|------:|--------:|----------:|------------:|
| **BuildBefore**       | **Build**       | **5**      | **Uniform**   |      **1.365 μs** |     **0.1915 μs** |   **0.0497 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Uniform   |      1.347 μs |     0.1235 μs |   0.0321 μs |  0.99 |    0.04 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **BuildBefore**       | **Build**       | **5**      | **LastVoxel** |      **3.602 μs** |     **0.1070 μs** |   **0.0278 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | LastVoxel |      1.438 μs |     0.0879 μs |   0.0136 μs |  0.40 |    0.00 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **BuildBefore**       | **Build**       | **5**      | **Runs**      |     **21.122 μs** |     **0.6452 μs** |   **0.1675 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Runs      |      5.744 μs |     0.1790 μs |   0.0277 μs |  0.27 |    0.00 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **BuildBefore**       | **Build**       | **5**      | **Dense**     |    **465.048 μs** |     **7.3561 μs** |   **1.1384 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Dense     |    315.146 μs |     5.3344 μs |   1.3853 μs |  0.68 |    0.00 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **BuildBefore**       | **Build**       | **7**      | **Uniform**   |    **104.593 μs** |    **12.2130 μs** |   **3.1717 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 7      | Uniform   |     99.350 μs |     7.8306 μs |   2.0336 μs |  0.95 |    0.03 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **BuildBefore**       | **Build**       | **7**      | **LastVoxel** |    **212.850 μs** |    **11.7566 μs** |   **3.0532 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 7      | LastVoxel |    104.317 μs |    29.4353 μs |   4.5551 μs |  0.49 |    0.02 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **BuildBefore**       | **Build**       | **7**      | **Runs**      |  **1,396.474 μs** |    **34.8862 μs** |   **9.0598 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 7      | Runs      |    426.163 μs |    13.3675 μs |   3.4715 μs |  0.31 |    0.00 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **BuildBefore**       | **Build**       | **7**      | **Dense**     | **29,597.240 μs** | **1,354.5093 μs** | **351.7618 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 7      | Dense     | 20,227.235 μs |   365.4501 μs |  56.5538 μs |  0.68 |    0.01 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Uniform**   |      **1.202 μs** |     **0.0613 μs** |   **0.0159 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | Uniform   |      1.185 μs |     0.0476 μs |   0.0124 μs |  0.99 |    0.02 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **LastVoxel** |      **1.641 μs** |     **0.0452 μs** |   **0.0117 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | LastVoxel |      1.354 μs |     0.0383 μs |   0.0059 μs |  0.82 |    0.01 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Runs**      |     **11.359 μs** |     **0.3898 μs** |   **0.1012 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | Runs      |      4.431 μs |     0.3027 μs |   0.0786 μs |  0.39 |    0.01 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Dense**     |     **54.311 μs** |     **1.1841 μs** |   **0.3075 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | Dense     |     18.026 μs |     0.6127 μs |   0.1591 μs |  0.33 |    0.00 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **7**      | **Uniform**   |     **96.360 μs** |    **27.6874 μs** |   **7.1903 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 7      | Uniform   |     98.295 μs |    43.7805 μs |  11.3697 μs |  1.02 |    0.13 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **7**      | **LastVoxel** |     **98.058 μs** |    **26.8091 μs** |   **6.9622 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 7      | LastVoxel |     87.451 μs |    17.0680 μs |   2.6413 μs |  0.90 |    0.06 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **7**      | **Runs**      |    **987.707 μs** |   **926.5114 μs** | **240.6121 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 7      | Runs      |    310.770 μs |    24.6616 μs |   6.4045 μs |  0.33 |    0.07 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **7**      | **Dense**     |  **3,747.114 μs** |    **81.9200 μs** |  **21.2744 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 7      | Dense     |  1,563.526 μs |   359.5244 μs |  93.3673 μs |  0.42 |    0.02 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Uniform**   |      **1.325 μs** |     **0.0702 μs** |   **0.0182 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Uniform   |      1.332 μs |     0.0813 μs |   0.0211 μs |  1.01 |    0.02 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **LastVoxel** |      **9.506 μs** |     **0.1786 μs** |   **0.0276 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | LastVoxel |      9.354 μs |     0.1604 μs |   0.0248 μs |  0.98 |    0.00 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Runs**      |     **31.598 μs** |     **1.2029 μs** |   **0.3124 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Runs      |     31.031 μs |     1.0195 μs |   0.2648 μs |  0.98 |    0.01 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Dense**     |    **443.109 μs** |    **14.1942 μs** |   **3.6862 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Dense     |    474.045 μs |    19.6503 μs |   5.1031 μs |  1.07 |    0.01 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **7**      | **Uniform**   |     **93.990 μs** |     **5.8328 μs** |   **1.5148 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 7      | Uniform   |     99.107 μs |    16.9311 μs |   4.3970 μs |  1.05 |    0.05 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **7**      | **LastVoxel** |    **351.208 μs** |    **32.6105 μs** |   **8.4688 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 7      | LastVoxel |    322.920 μs |    43.5213 μs |  11.3023 μs |  0.92 |    0.04 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **7**      | **Runs**      |  **2,293.057 μs** |    **17.7962 μs** |   **2.7540 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 7      | Runs      |  2,178.772 μs |    95.4273 μs |  24.7822 μs |  0.95 |    0.01 |         - |          NA |
|                   |             |        |           |               |               |             |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **7**      | **Dense**     | **28,765.412 μs** | **1,036.9354 μs** | **160.4669 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 7      | Dense     | 30,424.070 μs | 1,905.7679 μs | 494.9219 μs |  1.06 |    0.02 |         - |          NA |
