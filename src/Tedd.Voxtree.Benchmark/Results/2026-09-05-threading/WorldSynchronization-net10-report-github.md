```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 31.61 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-IJPESX : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

IterationCount=5  LaunchCount=1  WarmupCount=3

```
| Method        | Categories | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|-------------- |----------- |----------:|----------:|----------:|------:|----------:|------------:|
| ReadEach      | Read       | 26.130 ns | 1.0638 ns | 0.1646 ns |  1.00 |         - |          NA |
| ReadBatch     | Read       | 19.874 ns | 0.3556 ns | 0.0924 ns |  0.76 |         - |          NA |
| RetainedChunk | Read       |  1.887 ns | 0.0445 ns | 0.0116 ns |  0.07 |         - |          NA |
|               |            |           |           |           |       |           |             |
| WriteEach     | Write      | 27.335 ns | 0.5679 ns | 0.1475 ns |  1.00 |         - |          NA |
| WriteBatch    | Write      | 19.648 ns | 0.5869 ns | 0.1524 ns |  0.72 |         - |          NA |
