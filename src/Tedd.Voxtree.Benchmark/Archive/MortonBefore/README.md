# Morton conversion baseline

This directory freezes two conversion baselines from 2026-09-05:

- The ten original UInt32 and supporting files are from `377c360`, before the
  Morton conversion optimization.
- The nine generic files (`*Generic*.cs`, `VoxelType.cs`, `StorageInteger.cs`)
  are from `4575be5`, immediately after generic support was introduced and before
  applying the Morton optimization to that codec.

The C# files are copied from `src/Tedd.Voxtree` with only their namespace changed
to `Tedd.Voxtree.Benchmark.Archive.MortonBefore`. Supporting query and value types
are included to keep the baselines independent of later changes.
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

`GenericMortonConversions<T>` and `GenericMortonLayouts<T>` additionally compare
the archived and current generic APIs for `byte`, `ulong`, and `UInt128` on
32-cubed channels. Regression tests cover all five supported widths, including
opaque custom structs, on every supported target.

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
