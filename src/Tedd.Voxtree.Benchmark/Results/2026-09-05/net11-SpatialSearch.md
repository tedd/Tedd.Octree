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
| Method        | Categories | Pattern | Mean           | Error          | StdDev        | Ratio | Allocated | Alloc Ratio |
|-------------- |----------- |-------- |---------------:|---------------:|--------------:|------:|----------:|------------:|
| **PointCount**    | **Area**       | **Empty**   |   **7,010.572 ns** |    **233.0985 ns** |    **12.7769 ns** | **1.000** |         **-** |          **NA** |
| PrunedCount   | Area       | Empty   |       9.575 ns |      0.7853 ns |     0.0430 ns | 0.001 |         - |          NA |
|               |            |         |                |                |               |       |           |             |
| **PointCount**    | **Area**       | **Terrain** |  **72,130.135 ns** | **19,938.2137 ns** | **1,092.8808 ns** |  **1.00** |         **-** |          **NA** |
| PrunedCount   | Area       | Terrain |  10,363.028 ns |  1,616.4850 ns |    88.6050 ns |  0.14 |         - |          NA |
|               |            |         |                |                |               |       |           |             |
| **PointCount**    | **Area**       | **Sparse**  | **115,600.350 ns** | **24,481.7546 ns** | **1,341.9276 ns** |  **1.00** |         **-** |          **NA** |
| PrunedCount   | Area       | Sparse  |   9,872.328 ns |  1,347.2291 ns |    73.8462 ns |  0.09 |         - |          NA |
|               |            |         |                |                |               |       |           |             |
| **PointCount**    | **Area**       | **Dense**   |   **8,356.579 ns** |  **1,767.2273 ns** |    **96.8677 ns** |  **1.00** |         **-** |          **NA** |
| PrunedCount   | Area       | Dense   |     749.528 ns |    832.5776 ns |    45.6364 ns |  0.09 |         - |          NA |
|               |            |         |                |                |               |       |           |             |
| **PointNearest**  | **Nearest**    | **Empty**   |   **6,538.309 ns** | **15,108.4889 ns** |   **828.1473 ns** | **1.000** |         **-** |          **NA** |
| PrunedNearest | Nearest    | Empty   |      11.617 ns |      4.1143 ns |     0.2255 ns | 0.002 |         - |          NA |
|               |            |         |                |                |               |       |           |             |
| **PointNearest**  | **Nearest**    | **Terrain** |   **6,150.229 ns** |    **405.6470 ns** |    **22.2349 ns** |  **1.00** |         **-** |          **NA** |
| PrunedNearest | Nearest    | Terrain |   1,476.832 ns |     90.8941 ns |     4.9822 ns |  0.24 |         - |          NA |
|               |            |         |                |                |               |       |           |             |
| **PointNearest**  | **Nearest**    | **Sparse**  |  **13,410.786 ns** |  **1,966.9274 ns** |   **107.8139 ns** |  **1.00** |         **-** |          **NA** |
| PrunedNearest | Nearest    | Sparse  |   1,796.937 ns |     89.2654 ns |     4.8929 ns |  0.13 |         - |          NA |
|               |            |         |                |                |               |       |           |             |
| **PointNearest**  | **Nearest**    | **Dense**   |   **3,831.028 ns** |  **1,207.9217 ns** |    **66.2103 ns** | **1.000** |         **-** |          **NA** |
| PrunedNearest | Nearest    | Dense   |       7.056 ns |      0.2748 ns |     0.0151 ns | 0.002 |         - |          NA |
