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
| Method              | Mean        | Error     | StdDev    | Allocated |
|-------------------- |------------:|----------:|----------:|----------:|
| Unchanged           |    19.30 ns |  11.22 ns |  0.615 ns |         - |
| MoveOneVoxel        | 1,115.81 ns | 936.49 ns | 51.332 ns |         - |
| InvalidateAndRefill | 2,982.60 ns | 517.10 ns | 28.344 ns |         - |
