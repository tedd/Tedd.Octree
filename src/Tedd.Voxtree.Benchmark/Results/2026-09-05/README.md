# v2 reference measurements

These are BenchmarkDotNet ShortRun screening results collected on September 5,
2026, using the spatial-query milestone at commit ce6713f, before the later
bulk/streaming extension. Each report retains the runtime, SDK,
hardware, uncertainty, and allocation columns. A dash in Allocated means no
measured managed allocation, not missing data.

The `before-small-box-fast-path` report is the deliberate exception: it measures
`OctreeSpan.Any` dispatching every box through `OctreeQueries.State` and `Run`,
before adding the `StorageKind.Tree && box.Count <= 4` point-query branch.
That is the only library-code difference for the support comparison. The other
spatial suites do not measure `OctreeSpan.Any` and are unaffected by this branch.

Commands from the repository root:

```shell
dotnet run -c Release -f net10.0 --project src/Tedd.Voxtree.Benchmark -- --filter '*Spatial*' --job short
dotnet run -c Release -f net11.0 --project src/Tedd.Voxtree.Benchmark -- --filter '*Spatial*' --job short
dotnet run -c Release -f net10.0 --project src/Tedd.Voxtree.Benchmark -- --filter '*BuildOctrees*Levels: 5*' --job short
```

Build/lookup suites retain the archived v1 implementation as their baseline.
Spatial and channel suites specify separate point-query or packed-data baselines.
Data generation and parity checks are in each suite's setup and are excluded
from timing. Cache movement benchmarks include refresh costs, but provision
the cache buffer during setup.

ShortRun uses one launch and three measurement iterations. Host-load variance
is visible in several error columns. Use longer isolated runs to resolve small
differences; these separate runs do not establish a .NET 10-versus-11 ranking.
See [BENCHMARKS.md](../../BENCHMARKS.md) for hypotheses, datasets, and caveats.
