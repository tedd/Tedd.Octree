```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 30.69 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-ZNVNAZ : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

IterationCount=5  IterationTime=100ms  LaunchCount=1  
WarmupCount=3  

```
| Method         | Levels | Mean        | Error      | StdDev     | Allocated |
|--------------- |------- |------------:|-----------:|-----------:|----------:|
| **MortonToLinear** | **5**      |    **20.09 μs** |   **4.314 μs** |   **1.120 μs** |         **-** |
| LinearToMorton | 5      |    22.63 μs |   0.538 μs |   0.140 μs |         - |
| **MortonToLinear** | **7**      | **1,761.84 μs** | **491.743 μs** | **127.704 μs** |         **-** |
| LinearToMorton | 7      | 2,102.08 μs | 340.802 μs |  88.505 μs |         - |
