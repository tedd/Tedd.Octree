# Morton conversion baseline

This directory freezes the conversion implementation from commit
`377c360` (2026-09-05), immediately before the Morton conversion optimization.
The ten C# files are copied from `src/Tedd.Voxtree` with only their namespace
changed to `Tedd.Voxtree.Benchmark.Archive.MortonBefore`. Supporting query and
value types are included to keep the baseline independent of later changes.
Do not update this archive when optimizing the production implementation.

`Tests/MortonConversions.cs` benchmarks both implementations in the same run.
Methods ending in `Before` use this archive and are the BenchmarkDotNet baselines;
the other methods use the current library. Setup checks encoded-byte parity and
dense round trips. The cases cover 32-cubed and 128-cubed channels containing:

- Uniform values.
- A single different voxel at the end of Morton storage.
- Equal-value runs of length 1009, crossing octant boundaries.
- Deterministic random UInt32 data that selects dense fallback.

Both Morton and linear builds are measured to detect effects on the existing
linear path. `MortonLayouts` separately measures both permutation directions.
Input/output provisioning and setup validation are outside timed operations.

From the benchmark project directory, run:

```powershell
dotnet run -c Release -f net10.0 -- --filter '*MortonConversions*' '*MortonLayouts*' --job short --warmupCount 3 --iterationCount 5 --iterationTime 100 --launchCount 1
```

Use `net11.0` or `net8.0` to compare the other supported execution paths. For
closer results, use longer iterations and more launches. `Ratio` is current
time divided by archived time; values above 1 indicate a slower current result.
Retain the reported uncertainty when interpreting small differences.

The initial measurements are retained in
[`Results/2026-09-05-morton`](../../Results/2026-09-05-morton/README.md).
