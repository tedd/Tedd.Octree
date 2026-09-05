``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
AMD Ryzen 9 3950X, 1 CPU, 32 logical and 16 physical cores
.NET Core SDK=3.1.100
  [Host]     : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT
  Job-JTRQSC : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT

Runtime=.NET Core 3.1  

```
|       Method | Levels | AccessTimes |       Mean |     Error |    StdDev | Rank | Completed Work Items | Lock Contentions | Gen 0 | Gen 1 | Gen 2 | Allocated | Allocated native memory | Native memory leak |
|------------- |------- |------------ |-----------:|----------:|----------:|-----:|---------------------:|-----------------:|------:|------:|------:|----------:|------------------------:|-------------------:|
| **AccessOctree** |      **2** |        **1000** |  **40.757 us** | **0.0472 us** | **0.0394 us** |    **2** |               **0.0001** |                **-** |     **-** |     **-** |     **-** |         **-** |                    **43 B** |               **36 B** |
|  AccessArray |      2 |        1000 |         NA |        NA |        NA |    ? |                    - |                - |     - |     - |     - |         - |                       - |                  - |
| **AccessOctree** |      **3** |        **1000** |  **57.042 us** | **0.1733 us** | **0.1536 us** |    **3** |               **0.0001** |                **-** |     **-** |     **-** |     **-** |         **-** |                       **-** |                  **-** |
|  AccessArray |      3 |        1000 |         NA |        NA |        NA |    ? |                    - |                - |     - |     - |     - |         - |                       - |                  - |
| **AccessOctree** |      **4** |        **1000** |  **81.127 us** | **0.1227 us** | **0.1148 us** |    **4** |               **0.0002** |                **-** |     **-** |     **-** |     **-** |         **-** |                    **21 B** |                  **-** |
|  AccessArray |      4 |        1000 |   1.683 us | 0.0011 us | 0.0009 us |    1 |               0.0000 |                - |     - |     - |     - |         - |                       - |                  - |
| **AccessOctree** |      **5** |        **1000** |  **95.061 us** | **0.1124 us** | **0.0997 us** |    **5** |               **0.0002** |                **-** |     **-** |     **-** |     **-** |         **-** |                    **87 B** |               **72 B** |
|  AccessArray |      5 |        1000 |   1.681 us | 0.0019 us | 0.0017 us |    1 |               0.0000 |                - |     - |     - |     - |         - |                       - |                  - |
| **AccessOctree** |      **6** |        **1000** | **114.395 us** | **0.1984 us** | **0.1759 us** |    **6** |               **0.0002** |                **-** |     **-** |     **-** |     **-** |         **-** |                    **87 B** |               **72 B** |
|  AccessArray |      6 |        1000 |   1.681 us | 0.0021 us | 0.0019 us |    1 |               0.0000 |                - |     - |     - |     - |         - |                       - |                  - |

Benchmarks with issues:
  AccessOctrees.AccessArray: Job-JTRQSC(Runtime=.NET Core 3.1) [Levels=2, AccessTimes=1000]
  AccessOctrees.AccessArray: Job-JTRQSC(Runtime=.NET Core 3.1) [Levels=3, AccessTimes=1000]
