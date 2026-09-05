# Performance benchmarks

The benchmark project targets .NET 11 preview, .NET 10, and .NET 8, and uses
BenchmarkDotNet 0.16.0-preview.1 with its memory diagnoser. The .NET 8 host selects
the library's netstandard2.1 asset. The archived implementation is the baseline
in the original build/lookup suites; spatial suites have explicit point-query or
packed-channel baselines. Array operations are lower-bound controls, not
equivalent octree substitutes.

## Hypotheses

1. The caller-provided-span build path should report zero managed allocation
   after its destination buffer has been provisioned.
2. The optimized owned build should eliminate the v1 builder's transient node
   arrays; any remaining allocation should principally be the retained encoded
   tree.
3. Optimized owned and span-backed lookup should report zero managed allocation.
   The dense fallback should improve heterogeneous-data lookup latency; compressed
   tree latency is expected to depend on depth and data distribution.
4. Uniform input should compress to a constant-size root, spatially clustered
   input should collapse into a small number of homogeneous subtrees, sparse
   input should exercise pruning and mixed nodes, and random input should
   approximate the least-compressible case and select dense storage.
5. Sequential queries should expose cache locality; fixed pseudo-random queries
   should expose traversal and branch costs without benchmark-to-benchmark data
   drift.

These are falsifiable expectations, not acceptance thresholds. Compare both
time and Allocated columns, and retain the complete environment section emitted
by BenchmarkDotNet when recording results.

## Method

- Levels 3 and 5 cover small and moderate trees without making the v1 random
  build prohibitively slow.
- Uniform, sparse (approximately 1/64 populated), and random 30-bit data use a
  repository-local xorshift generator with fixed seeds. Clustered data uses
  three deterministic, power-of-two-aligned homogeneous cubes against a zero
  background: one half-width cube and two quarter-width cubes. This isolates
  the benefit of spatial coherence while retaining mixed branches.
- Each lookup invocation executes the same fixed sequence of 1,024 precomputed
  queries.
  BenchmarkDotNet normalizes the reported time per lookup.
- Global setup constructs every representation and verifies all measured
  queries against the source array before timing begins.
- GetRequiredSize and buffer allocation occur in global setup. The span-backed
  build therefore measures the intended steady-state reuse path.
- Owned v1 and v2 instances are also provisioned in global setup. Their timed
  Build calls include allocations performed by the build itself, including the
  retained encoded result.
- The array build control copies the same source into an existing destination.
  The array lookup control uses precomputed linear indices and consequently
  represents a practical lower bound.

The benchmark-only v1 source is under Archive/V1. It preserves the algorithm
from commit 3ac31f8 and replaces only the former Tedd helper packages with local
equivalent operations.

## Historical .NET 8 observations

A pre-modernization ShortRun measurement at commit 4b462d5 on .NET 8.0.30,
BenchmarkDotNet 0.15.8, Windows 11, and an AMD Ryzen 9
5950X produced the following level-5 results. ShortRun uses one launch and three
measurement iterations; these figures identify tendencies, not stable acceptance
thresholds.

- Caller-span builds allocated 0 B for every tested distribution.
- For random data, v2 owned build used 131,118 B versus v1's 1,155,047 B and
  measured 437 us versus 663 us. The v2 dense lookup measured 5.41 ns versus
  v1's 40.51 ns.
- For sparse data, v2 owned build used 9,480 B versus v1's 190,880 B. For
  clustered data it used 56 B versus 704 B.
- Compressed lookup was not uniformly faster: v2 measured 5.03 ns versus
  2.23 ns for uniform data, 40.05 ns versus 26.70 ns for sparse data, and
  12.11 ns versus 11.24 ns for clustered data.
- Every lookup case reported 0 B allocated. Owned-build latency varied by
  distribution; the allocation objective does not imply universal throughput
  superiority.

Re-run the complete matrix on the target machine before making deployment or
capacity decisions.

## Modern .NET 10 build observations

The level-5 ShortRun report is retained as
[net10-BuildOctrees.md](Results/2026-09-05/net10-BuildOctrees.md).
The modern uniform-region scan changes the trade-offs substantially:

- Uniform owned builds measured 1.345 us versus 209.068 us for v1; clustered
  owned builds measured 12.711 us versus 184.999 us.
- Sparse owned builds measured 325.647 us versus 249.435 us for v1, while
  allocating 9,480 B versus 190,880 B. Exact allocation requires a sizing pass
  and an encoding pass for compressed mixed trees. Reusable caller-span storage
  avoids the sizing pass and measured 166.718 us with 0 B allocated.
- Random owned builds measured 336.690 us versus 434.669 us, with approximately
  131 KB versus 1.16 MB allocated. The random caller-span path measured 436.625 us:
  its speculative tree writes are discarded before the dense fallback.
- Every caller-span build reported 0 B. Uniform/clustered gains do not imply
  universal build or point-lookup superiority.

## Commands

Run from the repository root:

    dotnet run -c Release -f net10.0 --project src/Tedd.Voxtree.Benchmark -- --list flat

Run the complete matrix:

    dotnet run -c Release -f net10.0 --project src/Tedd.Voxtree.Benchmark -- --filter "*"

Run one suite:

    dotnet run -c Release -f net10.0 --project src/Tedd.Voxtree.Benchmark -- --filter "*BuildOctrees*"
    dotnet run -c Release -f net10.0 --project src/Tedd.Voxtree.Benchmark -- --filter "*AccessOctrees*"

## Spatial and channel hypotheses

The recorded .NET 10/11 reports for the spatial milestone at commit ce6713f
(before the bulk/streaming extension) are under [Results/2026-09-05](Results/2026-09-05).
They retain environment information, error estimates, and allocation columns.
On the .NET 10 reference run:

- Nonempty area-count queries measured about 8-13x faster than repeated point
  reads; terrain/sparse nearest queries measured about 4-8x faster.
- Stationary terrain/sparse neighborhood reads improved about 4.7x/7.5x with
  caching. Dense point reads were faster without a cache. Moving-cache results
  include refresh costs and have wider uncertainty.
- An unchanged cache update measured 16 ns, a one-cell move 1.0 us, and full
  invalidation/refill 2.9 us for the measured sparse window.
- The final four-cell support query measured 57 ns stationary and 81 ns moving,
  versus 58/77 ns for point reads. A warm stationary cache measured 20 ns, but
  moving-cache maintenance raised a single check to 806 ns. Do not provision a
  moving cache solely for this one short query.
- Separate attribute channels used 21,856 encoded bytes versus 56,061 packed.
  Block-only reads measured 10.44 ns separate versus 24.73 ns packed; reading all
  attributes measured 57.63 ns separate versus 26.14 ns packed.
- All spatial queries and cache operations reported 0 B per operation. Owned
  channel builds allocated their final encoded arrays.

These measurements concern the synthetic workloads below, not every voxel
world, and small runtime-to-runtime differences require longer confirmation.

1. Area traversal should outperform repeated root-to-leaf point queries by
   skipping disjoint octants and counting homogeneous regions in bulk.
2. Nearest search should reduce work through octant distance bounds and an
   immediate hit when the query center itself matches.
3. A sliding dense cache should improve repeated reads of compressed data when
   reuse exceeds slab-refresh cost. Already-dense encodings can be faster to
   query directly; caching is opt-in.
4. Independent channels should improve compression and selective reads when
   attributes have different spatial patterns. Packed channels should reduce
   work when all attributes are read together.

Run the spatial suites separately on each runtime:

    dotnet run -c Release -f net10.0 --project src/Tedd.Voxtree.Benchmark -- --filter "*Spatial*" --job short
    dotnet run -c Release -f net11.0 --project src/Tedd.Voxtree.Benchmark -- --filter "*Spatial*" --job short

SpatialSearch compares 16-cubed box counting and radius-8 nearest searches with
equivalent point-query loops on a 32-cubed chunk. Empty, terrain, sparse, and
dense datasets have fixed seeds. Global setup verifies query parity.

SpatialNeighborhood measures 64 simulation steps per invocation, normalizing
time per step. Each step performs four passes over a 3-cubed neighborhood (108
reads). The cached variant includes Update, including new-slab refreshes; the
initial buffer allocation/refill occurs in setup. Moving steps follow a fixed
one-cell back-and-forth path, including endpoint pauses. These are warm-cache
workloads, not cold-memory or full-world simulations.

SpatialCacheMaintenance isolates unchanged windows, one-cell movement, and
explicit invalidation/full refill for an 8-cubed cache over sparse data.

SpatialSupport measures one 2-by-1-by-2 support-footprint test per simulation
step on terrain, using the same stationary/moving paths as SpatialNeighborhood.
It compares early-exit point reads, Any on the footprint, and cached Any including
Update. This deliberately low-reuse workload tests whether cache maintenance is
justified for a single falling/support decision.

The initial traversal-only Any implementation measured 266 ns for the stationary
four-cell footprint versus 73 ns for point reads. The final implementation uses
early-exit point reads for tree-backed boxes of at most four voxels, retaining
bulk traversal for larger boxes and uniform/dense encodings. The explicitly
labeled before-fast-path report preserves the rejected implementation's result.
In the .NET 10 rerun, stationary Any dropped to 57 ns and moving Any to 81 ns.
The paired point-read controls measured 58/77 ns; small differences are within
ShortRun uncertainty. This is a targeted improvement, not an argument for
point-by-point traversal of larger areas.

SpatialChannels compares four independent scalar channels (block, orientation,
fluid type, fluid amount) with the same attributes packed into one UInt32. It
separately measures block-only reads, all-channel reads, and owned builds.
Sources are precomputed, so build timings exclude packing/unpacking input arrays.
The fixed synthetic data is deliberately heterogeneous in orientation/fluid
amount while block/type regions remain coherent; conclusions apply to this
access pattern and distribution. Encoded payload size is printed during setup.

ShortRun results are screening evidence. Use the default longer job to confirm
small differences; preview-runtime and host-load variance can be substantial.

Pass any additional BenchmarkDotNet command-line options after the separator.
Do not compare Debug runs or runs conducted concurrently with material system
load.

## Dense-block and streaming hypotheses

1. Native Morton import should avoid the conversion pass and scratch buffer
   required by converting to linear order before building. Dense fallback may
   reverse this advantage because persisted dense values use linear order.
2. Regional extraction should beat repeated root-to-leaf lookups. Morton output
   can cost more than contiguous linear writes; neither layout is assumed faster
   for every workload. A uniform root can fill either order directly.
3. Known-empty outer worlds should be answered in constant time, independent
   of the logical voxel count, without a dense world-sized allocation.
4. Fixed-capacity arena branches and chunk-reference slots should permit
   allocation-free load/evict cycles and reuse after collapse.

The final bulk/world results and initial optimization comparison are retained
under [Results/2026-09-05-bulk](Results/2026-09-05-bulk). On the .NET 10 reference
machine, 32-cubed terrain Morton extraction measured 51.75 us versus 514.15 us for
point queries. Contiguous fills of aligned homogeneous Morton octants reduced
four-channel cross-chunk extraction from 204.47 to 14.55 us. Already-dense linear
extraction remained substantially faster than Morton output. All measured
bulk/world operations reported zero managed allocations after provisioning;
factories still allocate their documented snapshots and buffers.

`BulkBlocks` compares native linear builds, native Morton builds, and
Morton-to-linear conversion plus build, using preallocated destinations. Its
extraction cases compare linear bulk, Morton bulk, and Morton point-query loops.
Depths 3/5 select 8/32-cubed blocks, with terrain and dense distributions. Small
blocks use an unaligned origin inside the 32-cubed source. Setup validates parity.

`BulkWorld` holds eight four-channel chunks in world depths 10/16 and chunk
depths 3/5. It measures known-empty whole-world checks, resident point lookup,
all-channel extraction across eight chunks, region enumeration, and a paired
load/evict cycle. Chunk construction and index provisioning occur in setup;
streaming includes index mutation, but no I/O, encoding, or allocation of new
chunk payloads. This separates world lookup cost from chunk generation cost.

`BulkUniform` measures four-channel known-air extraction at depths 3/5 in both
layouts, including API validation and output writes. Its root-level fill path
does not enumerate voxels or calculate Morton indices.

    dotnet run -c Release -f net10.0 --project src/Tedd.Voxtree.Benchmark -- --filter '*Bulk*' --job short
    dotnet run -c Release -f net11.0 --project src/Tedd.Voxtree.Benchmark -- --filter '*Bulk*' --job short

Use complete reports and longer isolated confirmation runs for capacity or
deployment decisions. Morton input/output semantics use the package's actual
X-low-bit ordering and are checked against linear dense oracles in unit tests.
