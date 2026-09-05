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
| Method             | Levels | Layout | Mean       | Error       | StdDev    | Allocated |
|------------------- |------- |------- |-----------:|------------:|----------:|----------:|
| **ExtractAllChannels** | **3**      | **Linear** |   **105.7 ns** |    **16.66 ns** |   **0.91 ns** |         **-** |
| **ExtractAllChannels** | **3**      | **Morton** |   **101.5 ns** |    **17.25 ns** |   **0.95 ns** |         **-** |
| **ExtractAllChannels** | **5**      | **Linear** | **5,171.4 ns** | **1,923.94 ns** | **105.46 ns** |         **-** |
| **ExtractAllChannels** | **5**      | **Morton** | **5,313.4 ns** | **4,138.35 ns** | **226.84 ns** |         **-** |
