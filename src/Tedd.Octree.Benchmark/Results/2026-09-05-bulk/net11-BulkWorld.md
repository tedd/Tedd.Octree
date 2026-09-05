```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 34.33 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]   : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  ShortRun : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method           | WorldLevels | ChunkLevels | Mean          | Error          | StdDev      | Allocated |
|----------------- |------------ |------------ |--------------:|---------------:|------------:|----------:|
| EmptyWholeWorld  | 10          | 5           |      5.605 ns |      0.9150 ns |   0.0502 ns |         - |
| LoadedPoint      | 10          | 5           |     16.205 ns |      4.2448 ns |   0.2327 ns |         - |
| CrossChunkLinear | 10          | 5           | 38,042.598 ns | 10,498.9186 ns | 575.4812 ns |         - |
| CrossChunkMorton | 10          | 5           | 13,466.221 ns |    403.5899 ns |  22.1221 ns |         - |
| EnumerateRegions | 10          | 5           |    473.877 ns |    262.9387 ns |  14.4126 ns |         - |
| LoadAndEvict     | 10          | 5           |    189.918 ns |     55.1162 ns |   3.0211 ns |         - |
