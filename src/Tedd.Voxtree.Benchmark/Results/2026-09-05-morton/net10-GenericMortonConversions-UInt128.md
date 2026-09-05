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
| Method            | Categories  | Levels | Pattern   | Mean         | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------ |------------ |------- |---------- |-------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **BuildBefore**       | **Build**       | **5**      | **Uniform**   |    **13.558 μs** |  **0.3348 μs** | **0.0518 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Uniform   |    13.864 μs |  0.5254 μs | 0.1365 μs |  1.02 |    0.01 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **BuildBefore**       | **Build**       | **5**      | **LastVoxel** |    **30.019 μs** |  **0.7744 μs** | **0.2011 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | LastVoxel |    14.297 μs |  0.3857 μs | 0.0597 μs |  0.48 |    0.00 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **BuildBefore**       | **Build**       | **5**      | **Runs**      |    **41.028 μs** |  **1.2889 μs** | **0.3347 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Runs      |    20.328 μs |  1.0014 μs | 0.2601 μs |  0.50 |    0.01 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **BuildBefore**       | **Build**       | **5**      | **Dense**     | **1,027.379 μs** | **18.1913 μs** | **4.7242 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Dense     |   859.487 μs | 10.3366 μs | 2.6844 μs |  0.84 |    0.00 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Uniform**   |     **4.929 μs** |  **0.2015 μs** | **0.0523 μs** |  **1.00** |    **0.00** |      **80 B** |        **1.00** |
| ExtractMorton     | Extract     | 5      | Uniform   |     4.981 μs |  0.2852 μs | 0.0441 μs |  1.01 |    0.01 |         - |        0.00 |
|                   |             |        |           |              |            |           |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **LastVoxel** |     **5.574 μs** |  **0.0635 μs** | **0.0165 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | LastVoxel |     5.241 μs |  0.3289 μs | 0.0854 μs |  0.94 |    0.01 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Runs**      |    **16.343 μs** |  **1.0235 μs** | **0.2658 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | Runs      |     9.900 μs |  0.4693 μs | 0.1219 μs |  0.61 |    0.01 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Dense**     |    **71.220 μs** |  **1.8928 μs** | **0.2929 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | Dense     |    28.631 μs |  2.7138 μs | 0.7048 μs |  0.40 |    0.01 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Uniform**   |    **13.624 μs** |  **0.6735 μs** | **0.1042 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Uniform   |    13.910 μs |  0.2410 μs | 0.0626 μs |  1.02 |    0.01 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **LastVoxel** |    **33.327 μs** |  **0.6672 μs** | **0.1733 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | LastVoxel |    32.960 μs |  1.3823 μs | 0.3590 μs |  0.99 |    0.01 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Runs**      |    **39.422 μs** |  **2.5479 μs** | **0.6617 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Runs      |    38.161 μs |  1.1145 μs | 0.1725 μs |  0.97 |    0.02 |         - |          NA |
|                   |             |        |           |              |            |           |       |         |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Dense**     | **1,063.296 μs** | **30.2926 μs** | **7.8669 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Dense     | 1,024.836 μs | 30.6211 μs | 7.9522 μs |  0.96 |    0.01 |         - |          NA |
