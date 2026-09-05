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
| Method        | Levels | Pattern   | Mean          | Error         | StdDev      | Allocated |
|-------------- |------- |---------- |--------------:|--------------:|------------:|----------:|
| **BuildMorton**   | **5**      | **Uniform**   |      **1.351 μs** |     **0.0548 μs** |   **0.0142 μs** |         **-** |
| ExtractMorton | 5      | Uniform   |      1.176 μs |     0.0447 μs |   0.0069 μs |         - |
| **BuildMorton**   | **5**      | **LastVoxel** |      **3.571 μs** |     **0.1731 μs** |   **0.0450 μs** |         **-** |
| ExtractMorton | 5      | LastVoxel |      1.643 μs |     0.0797 μs |   0.0207 μs |         - |
| **BuildMorton**   | **5**      | **Runs**      |     **21.185 μs** |     **0.5220 μs** |   **0.1355 μs** |         **-** |
| ExtractMorton | 5      | Runs      |     11.284 μs |     0.3549 μs |   0.0922 μs |         - |
| **BuildMorton**   | **5**      | **Dense**     |    **461.470 μs** |    **13.1937 μs** |   **3.4264 μs** |         **-** |
| ExtractMorton | 5      | Dense     |     48.111 μs |     1.5679 μs |   0.4072 μs |         - |
| **BuildMorton**   | **7**      | **Uniform**   |     **91.934 μs** |     **4.1867 μs** |   **1.0873 μs** |         **-** |
| ExtractMorton | 7      | Uniform   |     93.637 μs |    17.4129 μs |   4.5221 μs |         - |
| **BuildMorton**   | **7**      | **LastVoxel** |    **215.570 μs** |    **58.2205 μs** |  **15.1197 μs** |         **-** |
| ExtractMorton | 7      | LastVoxel |    100.309 μs |     9.3927 μs |   2.4392 μs |         - |
| **BuildMorton**   | **7**      | **Runs**      |  **1,409.767 μs** |    **28.7681 μs** |   **4.4519 μs** |         **-** |
| ExtractMorton | 7      | Runs      |    770.994 μs |    16.8731 μs |   4.3819 μs |         - |
| **BuildMorton**   | **7**      | **Dense**     | **30,187.240 μs** | **2,476.7325 μs** | **643.1996 μs** |         **-** |
| ExtractMorton | 7      | Dense     |  3,564.713 μs |   176.4790 μs |  27.3103 μs |         - |
