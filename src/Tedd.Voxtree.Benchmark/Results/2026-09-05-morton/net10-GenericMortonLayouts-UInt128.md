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
| Method               | Categories | Levels | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------- |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| MortonToLinearBefore | ToLinear   | 5      | 29.43 μs | 2.093 μs | 0.544 μs |  1.00 |    0.00 |         - |          NA |
| MortonToLinear       | ToLinear   | 5      | 29.44 μs | 1.759 μs | 0.457 μs |  1.00 |    0.02 |         - |          NA |
|                      |            |        |          |          |          |       |         |           |             |
| LinearToMortonBefore | ToMorton   | 5      | 29.71 μs | 2.201 μs | 0.341 μs |  1.00 |    0.00 |         - |          NA |
| LinearToMorton       | ToMorton   | 5      | 29.30 μs | 1.819 μs | 0.472 μs |  0.99 |    0.02 |         - |          NA |
