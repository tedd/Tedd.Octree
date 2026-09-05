# Dense-block and streaming-world measurements

Reference hardware: AMD Ryzen 9 5950X, Windows 11. Runtime, SDK, uncertainty,
and allocation metadata are retained in each BenchmarkDotNet report. A dash in
Allocated means no measured managed allocation. Buffers and resident payloads
are provisioned in setup; these are warm-memory workloads, not disk/network I/O.

The final implementation is commit `7fa5d6b02ebd22c540c6271545afb609df380a60`.
The .NET 10 final run used its library/benchmark code before that commit, apart
from a subsequent whitespace-only indentation correction. The .NET 11 run and
the .NET 10 regression run use that commit's code. Documentation/report-only
commits do not change the measured implementation.

Reports suffixed `initial` are development measurements before the uniform-root
and aligned-Morton-octant fill optimizations. They are retained to distinguish
the initial result from the optimized result, not presented as release results.
The original v1 implementation remains independently runnable under
`Archive/V1`; the spatial milestone reports remain under `../2026-09-05`.

## .NET 10 dense-block results

| Workload | Measured mean | Interpretation |
| --- | --- | --- |
| 32-cubed terrain native Morton build | 41.35 us | Conversion then linear build took 64.42 us |
| 32-cubed terrain Morton extraction | 51.75 us | Point-query Morton loop took 514.15 us |
| 32-cubed dense linear extraction | 5.69 us | Morton extraction took 56.99 us; contiguous linear output remains preferable here |
| Four-channel 32-cubed known-air extraction | About 5.1 us | One contiguous fill per channel, either layout |
| Known-air 1024-cubed world check | 5.56 ns | Root metadata answers without expansion |
| Resident voxel in a 1024-cubed world | 19.45 ns | Outer lookup plus encoded channel lookup |
| Four-channel cross-chunk Morton extraction | 14.55 us | Initially 204.47 us before contiguous octant fills |
| Load and evict one existing chunk | 180.52 ns | Both index mutations included; payload creation and I/O excluded |

All final bulk/world cases measured zero managed allocations. Initial world
depths 10/16 both gave approximately 6 ns for known-empty whole-world checks;
the logical size alone does not add work when represented by one empty root.

An aligned homogeneous cube occupies one contiguous Morton interval. Filling
that interval reduced terrain extraction from 90.88 to 51.75 us and the flat
four-channel cross-chunk case from 204.47 to 14.55 us. Unaligned/clipped non-cubes
retain per-voxel Morton placement. Linear controls measured 59.80 to 58.48 us for
terrain and 40.04 to 37.27 us for the cross-chunk case. These are separate
ShortRun measurements, not paired statistical experiments.

Dense fallback still writes a persisted linear payload. Native Morton input
therefore does not guarantee faster builds: the dense case measured 488.01 us
versus 448.06 us for native linear input and 470.59 us for conversion plus build.
Do not generalize the coherent-terrain improvement to every data distribution.

## Final construction and support regression check

The final .NET 10 level-5 construction run preserves the earlier trade-offs:
caller-span builds allocate zero; owned sparse builds allocate 9,480 B versus
190,880 B for v1, but take 359.82 us versus 245.26 us. Reusing caller storage
avoids the sizing pass and measured 182.38 us. Random owned builds measured
365.85 us versus 444.32 us, with approximately 131 KB versus 1.16 MB allocated.
Uniform/clustered owned builds measured 1.51/13.68 us versus 184.39/188.21 us.

The four-cell support check measured 61.40 ns stationary and 83.70 ns moving,
versus 60.77/80.75 ns for point reads. Cached support measured 20.04 ns stationary
but 837.78 ns moving, including refresh. Every support case allocated zero.
This still argues against maintaining a moving dense cache for just one tiny
occupancy query. Compared with the prior milestone, several build means rose
by roughly 8-12%; the separate ShortRun intervals overlap, so neither a proven
regression nor strict performance parity is claimed.

## .NET 11 preview check

The .NET 11 preview run also measured zero allocations throughout the 22 final
bulk cases. Its terrain Morton extraction measured 46.37 us versus 480.72 us for
point queries, and four-channel cross-chunk Morton extraction measured 13.47 us.
The dense-output layout trade-off remained: linear 5.43 us versus Morton
61.93 us. Each report gives its own confidence intervals; these separate host
runs are not evidence for a general runtime ranking.

## Reproduction

Run from the repository root, one runtime at a time:

```shell
dotnet run -c Release -f net10.0 --project src/Tedd.Octree.Benchmark -- --filter '*Bulk*' --job short
dotnet run -c Release -f net11.0 --project src/Tedd.Octree.Benchmark -- --filter '*BulkBlocks*Levels: 5*' '*BulkWorld*WorldLevels: 10, ChunkLevels: 5*' '*BulkUniform*' --job short
dotnet run -c Release -f net10.0 --project src/Tedd.Octree.Benchmark -- --filter '*BuildOctrees*Levels: 5*' '*SpatialSupport*' --job short
```

The final .NET 10 bulk rerun used the same 22-case filter as the .NET 11 command.
The initial .NET 10 matrix had 48 cases; `BulkUniform` was added afterward.
ShortRun uses one launch and three measurements. Several intervals are wide;
use default longer jobs to confirm small differences. These reports do not
establish a .NET 10-versus-.NET 11 ranking or cold-memory throughput guarantees.
