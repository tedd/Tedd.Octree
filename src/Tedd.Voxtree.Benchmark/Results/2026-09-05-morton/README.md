# Morton conversion benchmarks — 2026-09-05

Native Morton reconstruction and decoding preserve the existing byte formats and avoid a linear scratch buffer. The generic codec now uses the same approach for every supported width. This directory retains the original measurements, the intermediate run that exposed a linear-build regression, the final paired comparison, and a longer rerun of the noisy generic layout cases.

## Environment and protocol

- BenchmarkDotNet 0.16.0-preview.1; .NET 10.0.11, x64 RyuJIT, workstation GC.
- AMD Ryzen 9 5950X, Windows 11; SDK 11.0.100-preview.7.26381.103.
- Final comparison: 140 benchmark cases, 1 launch, 3 warmups, 5 measured iterations, 100 ms target iteration time.
- Generic layout confirmation: 12 cases, 2 launches, 5 warmups, 12 measured iterations, 250 ms target iteration time.
- Setup allocates buffers and verifies archived/current encoded bytes and decoded voxel bits; timed calls reuse those buffers.
- Every current implementation case reported **0 B allocated per operation**.

The archived UInt32 implementation is from 377c360; the generic implementation is from 4575be5. All 19 archived C# files were verified against those commits with only namespace and line-ending differences. See the [archive and rerun instructions](../../Archive/MortonBefore/README.md).

## Native UInt32 comparison

Ratios below are current time / archived time. **Below 1 is faster; above 1 is slower.** The linked full reports contain means, uncertainty, and allocation measurements.

| Side | Pattern | Morton build | Morton extraction | Linear build |
| --- | --- | ---: | ---: | ---: |
| 32 | Uniform | 1.02 | 1.01 | 1.01 |
| 32 | LastVoxel | 0.40 | 0.81 | 0.91 |
| 32 | Runs | 0.31 | 0.39 | 0.99 |
| 32 | Dense | 0.59 | 0.44 | 1.02 |
| 128 | Uniform | 1.04 | 1.01 | 0.84 |
| 128 | LastVoxel | 0.45 | 0.63 | 1.12 |
| 128 | Runs | 0.33 | 0.37 | 1.03 |
| 128 | Dense | 0.67 | 0.45 | 0.97 |

The run-heavy UInt32 cases rebuild in roughly one-third of the archived time and extract in roughly 37–39% of that time. Constant-volume operations are already dominated by scanning/filling memory.

## Generic comparison (32-cubed)

| Storage | Pattern | Morton build | Morton extraction | Linear build |
| --- | --- | ---: | ---: | ---: |
| Byte | Uniform | 0.05 | 1.07 | 1.03 |
| Byte | LastVoxel | 0.03 | 0.58 | 1.03 |
| Byte | Runs | 0.15 | 0.42 | 0.95 |
| Byte | Dense | 0.57 | 0.32 | 0.93 |
| UInt64 | Uniform | 0.39 | 0.98 | 0.99 |
| UInt64 | LastVoxel | 0.18 | 0.85 | 1.00 |
| UInt64 | Runs | 0.26 | 0.52 | 1.00 |
| UInt64 | Dense | 0.75 | 0.32 | 0.97 |
| UInt128 | Uniform | 1.02 | 1.01 | 1.02 |
| UInt128 | LastVoxel | 0.48 | 0.94 | 0.99 |
| UInt128 | Runs | 0.50 | 0.61 | 0.97 |
| UInt128 | Dense | 0.84 | 0.40 | 0.96 |

The generic uniform-byte extraction case changed from 285.2 ns to 303.9 ns (about 19 ns slower), while removing the archived path's 80 B allocation. The allocation came from comparing boxed region bounds.

## Longer layout-conversion rerun

| Storage | Direction | Archived mean | Current mean | Ratio |
| --- | --- | ---: | ---: | ---: |
| Byte | MortonToLinear | 21.79 μs | 17.03 μs | 0.78 |
| Byte | LinearToMorton | 19.67 μs | 18.92 μs | 0.96 |
| UInt64 | MortonToLinear | 21.67 μs | 20.56 μs | 0.95 |
| UInt64 | LinearToMorton | 20.21 μs | 22.27 μs | 1.10 |
| UInt128 | MortonToLinear | 31.14 μs | 32.96 μs | 1.06 |
| UInt128 | LinearToMorton | 31.77 μs | 31.99 μs | 1.01 |

The short run suggested a 48% slowdown for generic UInt64 linear-to-Morton conversion. The longer run measured 10%, with overlapping 99.9% confidence intervals; the original magnitude did not reproduce. UInt128 Morton-to-linear was 6% slower by mean in the longer run, also with overlapping intervals. These layout cases are inconclusive; the data do not establish a universal permutation speedup.

## Regression assessment and validation

- The intermediate UInt32 dense linear-build ratios were 1.07 and 1.06. Moving layout dispatch out of recursion and inlining node emission changed the final ratios to 1.02 and 0.97; the final uncertainty does not establish a dense linear-build regression.
- The 128-cubed sparse linear-build case measured ratio 1.12 with wide, overlapping intervals. Several large-buffer cases also have substantial variance. Treat exact speedup magnitudes as local measurements, not guarantees.
- Full tests passed on .NET 11, .NET 10, and the .NET Standard 2.1 library under .NET 8: **212 per target, 636 total**.
- Tests cover both serialized formats, all five generic storage widths, raw custom-struct bits, run boundaries, single-voxel edits, dense fallback, aligned/unaligned blocks, chunk/channel offsets, malformed data, buffer bounds, aliasing, and allocation behavior.
- Final tests used an isolated snapshot of 4575be5 plus the Morton changes to exclude concurrent entity work. The final benchmark suite ran the same conversion sources from the benchmark project directory.
- Benchmark timings here are for .NET 10 only; the other runtimes were tested for correctness.

## Retained reports

| Phase | Reports (CSV files accompany every Markdown report) |
| --- | --- |
| Before any implementation edits | [UInt32 builds/extraction](before-MortonConversions.md), [layouts](before-MortonLayouts.md) |
| Intermediate paired run | [UInt32 builds/extraction](intermediate-MortonConversions.md), [layouts](intermediate-MortonLayouts.md) |
| Final UInt32 | [builds/extraction](net10-MortonConversions.md), [layouts](net10-MortonLayouts.md) |
| Final Byte | [builds/extraction](net10-GenericMortonConversions-Byte.md), [layouts](net10-GenericMortonLayouts-Byte.md), [longer layout rerun](confirmation-net10-GenericMortonLayouts-Byte.md) |
| Final UInt64 | [builds/extraction](net10-GenericMortonConversions-UInt64.md), [layouts](net10-GenericMortonLayouts-UInt64.md), [longer layout rerun](confirmation-net10-GenericMortonLayouts-UInt64.md) |
| Final UInt128 | [builds/extraction](net10-GenericMortonConversions-UInt128.md), [layouts](net10-GenericMortonLayouts-UInt128.md), [longer layout rerun](confirmation-net10-GenericMortonLayouts-UInt128.md) |

## Reproduce

Run from the src/Tedd.Voxtree.Benchmark directory:

```powershell
dotnet run -c Release -f net10.0 -- --filter '*MortonConversions*' '*MortonLayouts*' --job short --warmupCount 3 --iterationCount 5 --iterationTime 100 --launchCount 1
dotnet run -c Release -f net10.0 -- --filter '*GenericMortonLayouts*' --job short --warmupCount 5 --iterationCount 12 --iterationTime 250 --launchCount 2
```
