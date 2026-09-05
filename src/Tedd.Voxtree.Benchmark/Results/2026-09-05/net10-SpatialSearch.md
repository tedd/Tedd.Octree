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
| Method        | Categories | Pattern | Mean           | Error          | StdDev         | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------- |----------- |-------- |---------------:|---------------:|---------------:|------:|--------:|----------:|------------:|
| **PointCount**    | **Area**       | **Empty**   |   **8,712.216 ns** |   **2,729.012 ns** |    **149.5863 ns** | **1.000** |    **0.00** |         **-** |          **NA** |
| PrunedCount   | Area       | Empty   |      12.019 ns |       1.773 ns |      0.0972 ns | 0.001 |    0.00 |         - |          NA |
|               |            |         |                |                |                |       |         |           |             |
| **PointCount**    | **Area**       | **Terrain** | **122,266.764 ns** |  **57,412.806 ns** |  **3,146.9896 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| PrunedCount   | Area       | Terrain |  14,735.871 ns |  13,500.588 ns |    740.0128 ns |  0.12 |    0.01 |         - |          NA |
|               |            |         |                |                |                |       |         |           |             |
| **PointCount**    | **Area**       | **Sparse**  | **133,602.336 ns** | **456,798.005 ns** | **25,038.6401 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| PrunedCount   | Area       | Sparse  |  11,328.435 ns |  13,320.683 ns |    730.1516 ns |  0.09 |    0.01 |         - |          NA |
|               |            |         |                |                |                |       |         |           |             |
| **PointCount**    | **Area**       | **Dense**   |   **9,698.931 ns** |   **1,447.442 ns** |     **79.3392 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| PrunedCount   | Area       | Dense   |     759.983 ns |     324.235 ns |     17.7724 ns |  0.08 |    0.00 |         - |          NA |
|               |            |         |                |                |                |       |         |           |             |
| **PointNearest**  | **Nearest**    | **Empty**   |   **6,963.466 ns** |   **2,969.595 ns** |    **162.7735 ns** | **1.000** |    **0.00** |         **-** |          **NA** |
| PrunedNearest | Nearest    | Empty   |      15.261 ns |       8.063 ns |      0.4420 ns | 0.002 |    0.00 |         - |          NA |
|               |            |         |                |                |                |       |         |           |             |
| **PointNearest**  | **Nearest**    | **Terrain** |   **8,304.897 ns** |   **1,536.668 ns** |     **84.2299 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| PrunedNearest | Nearest    | Terrain |   1,883.020 ns |   3,327.767 ns |    182.4061 ns |  0.23 |    0.02 |         - |          NA |
|               |            |         |                |                |                |       |         |           |             |
| **PointNearest**  | **Nearest**    | **Sparse**  |  **16,075.288 ns** |   **2,875.317 ns** |    **157.6058 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| PrunedNearest | Nearest    | Sparse  |   2,034.708 ns |     475.459 ns |     26.0615 ns |  0.13 |    0.00 |         - |          NA |
|               |            |         |                |                |                |       |         |           |             |
| **PointNearest**  | **Nearest**    | **Dense**   |   **4,531.617 ns** |   **2,058.111 ns** |    **112.8120 ns** | **1.000** |    **0.00** |         **-** |          **NA** |
| PrunedNearest | Nearest    | Dense   |       8.480 ns |       6.261 ns |      0.3432 ns | 0.002 |    0.00 |         - |          NA |
