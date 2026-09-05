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
| Method             | Levels | Layout | Mean       | Error       | StdDev    | Allocated |
|------------------- |------- |------- |-----------:|------------:|----------:|----------:|
| **ExtractAllChannels** | **3**      | **Linear** |   **177.7 ns** |   **401.78 ns** |  **22.02 ns** |         **-** |
| **ExtractAllChannels** | **3**      | **Morton** |   **141.1 ns** |     **8.08 ns** |   **0.44 ns** |         **-** |
| **ExtractAllChannels** | **5**      | **Linear** | **5,059.3 ns** |   **404.61 ns** |  **22.18 ns** |         **-** |
| **ExtractAllChannels** | **5**      | **Morton** | **5,158.5 ns** | **2,319.71 ns** | **127.15 ns** |         **-** |
