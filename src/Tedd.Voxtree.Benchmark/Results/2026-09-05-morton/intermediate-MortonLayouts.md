```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 30.12 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-ZNVNAZ : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

IterationCount=5  IterationTime=100ms  LaunchCount=1  
WarmupCount=3  

```
| Method               | Categories | Levels | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **MortonToLinearBefore** | **ToLinear**   | **5**      |    **20.70 μs** |   **1.206 μs** |  **0.313 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonToLinear       | ToLinear   | 5      |    17.91 μs |   0.879 μs |  0.228 μs |  0.87 |    0.02 |         - |          NA |
|                      |            |        |             |            |           |       |         |           |             |
| **MortonToLinearBefore** | **ToLinear**   | **7**      | **1,497.03 μs** | **135.513 μs** | **35.192 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| MortonToLinear       | ToLinear   | 7      | 1,478.66 μs | 153.779 μs | 39.936 μs |  0.99 |    0.03 |         - |          NA |
|                      |            |        |             |            |           |       |         |           |             |
| **LinearToMortonBefore** | **ToMorton**   | **5**      |    **19.91 μs** |   **1.246 μs** |  **0.324 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearToMorton       | ToMorton   | 5      |    18.00 μs |   2.387 μs |  0.369 μs |  0.90 |    0.02 |         - |          NA |
|                      |            |        |             |            |           |       |         |           |             |
| **LinearToMortonBefore** | **ToMorton**   | **7**      | **1,475.07 μs** | **223.645 μs** | **58.080 μs** |  **1.00** |    **0.00** |         **-** |          **NA** |
| LinearToMorton       | ToMorton   | 7      | 1,411.45 μs |  81.139 μs | 21.072 μs |  0.96 |    0.04 |         - |          NA |
