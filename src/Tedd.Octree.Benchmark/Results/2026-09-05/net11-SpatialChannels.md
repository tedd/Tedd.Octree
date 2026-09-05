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
| Method             | Categories | Mean          | Error         | StdDev       | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |--------------:|--------------:|-------------:|------:|--------:|-------:|-------:|----------:|------------:|
| PackedAllReads     | AllReads   |      39.40 ns |    136.797 ns |     7.498 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| SeparateAllReads   | AllReads   |      86.70 ns |     47.316 ns |     2.594 ns |  2.26 |    0.42 |      - |      - |         - |          NA |
|                    |            |               |               |              |       |         |        |        |           |             |
| PackedBlockReads   | BlockRead  |      31.42 ns |     27.827 ns |     1.525 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| SeparateBlockReads | BlockRead  |      17.92 ns |      9.157 ns |     0.502 ns |  0.57 |    0.03 |      - |      - |         - |          NA |
|                    |            |               |               |              |       |         |        |        |           |             |
| PackedBuild        | Build      | 577,308.43 ns | 50,158.404 ns | 2,749.351 ns |  1.00 |    0.00 | 2.9297 | 0.4883 |   56088 B |        1.00 |
| SeparateBuild      | Build      | 572,839.11 ns | 65,571.588 ns | 3,594.200 ns |  0.99 |    0.01 | 0.9766 |      - |   21968 B |        0.39 |
