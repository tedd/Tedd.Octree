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
| Method            | Categories  | Levels | Pattern   | Mean         | Error        | StdDev      | Ratio | Gen0   | Allocated | Alloc Ratio |
|------------------ |------------ |------- |---------- |-------------:|-------------:|------------:|------:|-------:|----------:|------------:|
| **BuildBefore**       | **Build**       | **5**      | **Uniform**   |   **7,078.5 ns** |    **103.43 ns** |    **16.01 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Uniform   |     348.7 ns |     14.74 ns |     3.83 ns |  0.05 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **BuildBefore**       | **Build**       | **5**      | **LastVoxel** |  **15,836.1 ns** |    **736.58 ns** |   **191.29 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | LastVoxel |     471.1 ns |     17.08 ns |     4.43 ns |  0.03 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **BuildBefore**       | **Build**       | **5**      | **Runs**      |  **32,616.1 ns** |  **2,741.21 ns** |   **711.88 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Runs      |   4,909.0 ns |    114.30 ns |    29.68 ns |  0.15 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **BuildBefore**       | **Build**       | **5**      | **Dense**     | **303,304.4 ns** | **10,717.74 ns** | **1,658.58 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| BuildMorton       | Build       | 5      | Dense     | 172,956.6 ns |  7,495.60 ns | 1,946.58 ns |  0.57 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Uniform**   |     **285.2 ns** |      **8.69 ns** |     **2.26 ns** |  **1.00** | **0.0028** |      **80 B** |        **1.00** |
| ExtractMorton     | Extract     | 5      | Uniform   |     303.9 ns |      3.47 ns |     0.54 ns |  1.07 |      - |         - |        0.00 |
|                   |             |        |           |              |              |             |       |        |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **LastVoxel** |     **822.5 ns** |     **17.20 ns** |     **4.47 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | LastVoxel |     478.7 ns |     10.85 ns |     2.82 ns |  0.58 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Runs**      |  **10,943.0 ns** |    **199.11 ns** |    **51.71 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | Runs      |   4,565.2 ns |     88.72 ns |    23.04 ns |  0.42 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **ExtractBefore**     | **Extract**     | **5**      | **Dense**     |  **65,355.3 ns** |  **3,342.37 ns** |   **517.24 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| ExtractMorton     | Extract     | 5      | Dense     |  21,076.6 ns |    849.77 ns |   131.50 ns |  0.32 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Uniform**   |   **7,039.0 ns** |    **249.85 ns** |    **64.88 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Uniform   |   7,229.1 ns |     96.95 ns |    15.00 ns |  1.03 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **LastVoxel** |  **17,662.9 ns** |    **550.27 ns** |   **142.90 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | LastVoxel |  18,254.7 ns |    654.13 ns |   169.87 ns |  1.03 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Runs**      |  **29,577.4 ns** |    **371.03 ns** |    **57.42 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Runs      |  28,098.4 ns |  1,132.32 ns |   294.06 ns |  0.95 |      - |         - |          NA |
|                   |             |        |           |              |              |             |       |        |           |             |
| **LinearBuildBefore** | **LinearBuild** | **5**      | **Dense**     | **245,163.9 ns** |  **4,468.67 ns** | **1,160.50 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| LinearBuild       | LinearBuild | 5      | Dense     | 228,877.8 ns |  6,034.42 ns | 1,567.12 ns |  0.93 |      - |         - |          NA |
