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
| Method           | WorldLevels | ChunkLevels | Mean          | Error         | StdDev      | Allocated |
|----------------- |------------ |------------ |--------------:|--------------:|------------:|----------:|
| EmptyWholeWorld  | 10          | 5           |      5.557 ns |      1.130 ns |   0.0619 ns |         - |
| LoadedPoint      | 10          | 5           |     19.447 ns |      9.380 ns |   0.5141 ns |         - |
| CrossChunkLinear | 10          | 5           | 37,273.317 ns |  5,377.473 ns | 294.7575 ns |         - |
| CrossChunkMorton | 10          | 5           | 14,554.954 ns | 16,317.759 ns | 894.4314 ns |         - |
| EnumerateRegions | 10          | 5           |    471.092 ns |    769.283 ns |  42.1670 ns |         - |
| LoadAndEvict     | 10          | 5           |    180.517 ns |     54.710 ns |   2.9988 ns |         - |
