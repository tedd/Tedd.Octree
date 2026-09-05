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
| Method              | Mean        | Error      | StdDev    | Allocated |
|-------------------- |------------:|-----------:|----------:|----------:|
| Unchanged           |    16.15 ns |   0.600 ns |  0.033 ns |         - |
| MoveOneVoxel        | 1,024.42 ns |  41.041 ns |  2.250 ns |         - |
| InvalidateAndRefill | 2,882.48 ns | 558.180 ns | 30.596 ns |         - |
