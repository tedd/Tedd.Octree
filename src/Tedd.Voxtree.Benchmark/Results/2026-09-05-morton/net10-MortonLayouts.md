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
| Method               | Categories | Levels | Mean        | Error        | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------- |------------:|-------------:|-----------:|------:|--------:|----------:|------------:|
| **MortonToLinearBefore** | **ToLinear**   | **5**      |    **19.57 μs** |     **1.528 μs** |   **0.397 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonToLinear       | ToLinear   | 5      |    17.41 μs |     0.547 μs |   0.142 μs |  0.89 |    0.02 |         - |          NA |
|                      |            |        |             |              |            |       |         |           |             |
| **MortonToLinearBefore** | **ToLinear**   | **7**      | **2,307.01 μs** | **1,818.124 μs** | **472.161 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonToLinear       | ToLinear   | 7      | 1,782.91 μs |   767.574 μs | 118.783 μs |  0.80 |    0.14 |         - |          NA |
|                      |            |        |             |              |            |       |         |           |             |
| **LinearToMortonBefore** | **ToMorton**   | **5**      |    **21.65 μs** |     **5.274 μs** |   **1.370 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearToMorton       | ToMorton   | 5      |    18.15 μs |     1.164 μs |   0.302 μs |  0.84 |    0.05 |         - |          NA |
|                      |            |        |             |              |            |       |         |           |             |
| **LinearToMortonBefore** | **ToMorton**   | **7**      | **2,240.85 μs** |   **253.176 μs** |  **65.749 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearToMorton       | ToMorton   | 7      | 1,843.42 μs | 1,101.704 μs | 286.109 μs |  0.82 |    0.12 |         - |          NA |
