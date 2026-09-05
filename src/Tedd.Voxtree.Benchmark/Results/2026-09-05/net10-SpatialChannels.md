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
| Method             | Categories | Mean          | Error         | StdDev       | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |--------------:|--------------:|-------------:|------:|--------:|-------:|-------:|----------:|------------:|
| PackedAllReads     | AllReads   |      26.14 ns |      8.421 ns |     0.462 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| SeparateAllReads   | AllReads   |      57.63 ns |      3.696 ns |     0.203 ns |  2.21 |    0.03 |      - |      - |         - |          NA |
|                    |            |               |               |              |       |         |        |        |           |             |
| PackedBlockReads   | BlockRead  |      24.73 ns |      5.714 ns |     0.313 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| SeparateBlockReads | BlockRead  |      10.44 ns |      0.245 ns |     0.013 ns |  0.42 |    0.00 |      - |      - |         - |          NA |
|                    |            |               |               |              |       |         |        |        |           |             |
| PackedBuild        | Build      | 410,252.33 ns | 58,211.510 ns | 3,190.769 ns |  1.00 |    0.00 | 2.9297 | 0.4883 |   56088 B |        1.00 |
| SeparateBuild      | Build      | 365,951.11 ns | 46,435.814 ns | 2,545.304 ns |  0.89 |    0.01 | 0.9766 |      - |   21968 B |        0.39 |
