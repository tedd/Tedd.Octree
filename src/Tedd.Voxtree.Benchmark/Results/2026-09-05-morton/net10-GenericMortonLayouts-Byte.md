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
| MortonToLinearBefore | ToLinear   | 5      | 22.54 μs | 1.736 μs | 0.451 μs |  1.00 |    0.00 |         - |          NA |
| MortonToLinear       | ToLinear   | 5      | 16.92 μs | 0.709 μs | 0.184 μs |  0.75 |    0.02 |         - |          NA |
|                      |            |        |          |          |          |       |         |           |             |
| LinearToMortonBefore | ToMorton   | 5      | 19.52 μs | 0.999 μs | 0.155 μs |  1.00 |    0.00 |         - |          NA |
| LinearToMorton       | ToMorton   | 5      | 21.18 μs | 1.109 μs | 0.172 μs |  1.08 |    0.01 |         - |          NA |
