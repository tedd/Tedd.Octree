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
| Method           | WorldLevels | ChunkLevels | Mean           | Error           | StdDev         | Allocated |
|----------------- |------------ |------------ |---------------:|----------------:|---------------:|----------:|
| **EmptyWholeWorld**  | **10**          | **3**           |       **5.791 ns** |       **4.4459 ns** |      **0.2437 ns** |         **-** |
| LoadedPoint      | 10          | 3           |      22.790 ns |       8.5662 ns |      0.4695 ns |         - |
| CrossChunkLinear | 10          | 3           |   7,001.456 ns |   1,545.4856 ns |     84.7133 ns |         - |
| CrossChunkMorton | 10          | 3           |   9,775.336 ns |   3,696.2399 ns |    202.6034 ns |         - |
| EnumerateRegions | 10          | 3           |     626.125 ns |      97.0495 ns |      5.3196 ns |         - |
| LoadAndEvict     | 10          | 3           |     241.257 ns |       6.1761 ns |      0.3385 ns |         - |
| **EmptyWholeWorld**  | **10**          | **5**           |       **5.571 ns** |       **0.6761 ns** |      **0.0371 ns** |         **-** |
| LoadedPoint      | 10          | 5           |      18.859 ns |       4.8096 ns |      0.2636 ns |         - |
| CrossChunkLinear | 10          | 5           |  40,036.308 ns |   3,919.5799 ns |    214.8454 ns |         - |
| CrossChunkMorton | 10          | 5           | 204,472.485 ns |  72,521.0753 ns |  3,975.1249 ns |         - |
| EnumerateRegions | 10          | 5           |     490.317 ns |     636.9084 ns |     34.9111 ns |         - |
| LoadAndEvict     | 10          | 5           |     180.958 ns |      92.2971 ns |      5.0591 ns |         - |
| **EmptyWholeWorld**  | **16**          | **3**           |       **5.713 ns** |       **2.9471 ns** |      **0.1615 ns** |         **-** |
| LoadedPoint      | 16          | 3           |      30.147 ns |       1.4742 ns |      0.0808 ns |         - |
| CrossChunkLinear | 16          | 3           |   8,916.880 ns |     723.9115 ns |     39.6800 ns |         - |
| CrossChunkMorton | 16          | 3           |  11,948.455 ns |   1,928.5392 ns |    105.7097 ns |         - |
| EnumerateRegions | 16          | 3           |   1,274.678 ns |   1,164.2296 ns |     63.8154 ns |         - |
| LoadAndEvict     | 16          | 3           |     548.003 ns |   1,119.9021 ns |     61.3856 ns |         - |
| **EmptyWholeWorld**  | **16**          | **5**           |       **6.105 ns** |       **2.7738 ns** |      **0.1520 ns** |         **-** |
| LoadedPoint      | 16          | 5           |      29.433 ns |      13.9467 ns |      0.7645 ns |         - |
| CrossChunkLinear | 16          | 5           |  45,537.832 ns |  12,637.1990 ns |    692.6875 ns |         - |
| CrossChunkMorton | 16          | 5           | 222,496.346 ns | 307,123.6780 ns | 16,834.4852 ns |         - |
| EnumerateRegions | 16          | 5           |   1,009.625 ns |     513.0127 ns |     28.1200 ns |         - |
| LoadAndEvict     | 16          | 5           |     347.229 ns |      88.5874 ns |      4.8558 ns |         - |
