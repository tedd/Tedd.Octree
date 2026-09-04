# Performance benchmarks

The benchmark project targets .NET 8 and uses BenchmarkDotNet 0.15.8 with its
memory diagnoser. The archived implementation is the baseline in every
parameter group. Array operations are lower-bound controls, not equivalent
octree substitutes.

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

## Reference observations

A ShortRun screening measurement on .NET 8.0.30, Windows 11, and an AMD Ryzen 9
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

## Commands

Run from the repository root:

    dotnet run -c Release --project src/Tedd.Octree.Benchmark -- --list flat

Run the complete matrix:

    dotnet run -c Release --project src/Tedd.Octree.Benchmark -- --filter "*"

Run one suite:

    dotnet run -c Release --project src/Tedd.Octree.Benchmark -- --filter "*BuildOctrees*"
    dotnet run -c Release --project src/Tedd.Octree.Benchmark -- --filter "*AccessOctrees*"

Pass any additional BenchmarkDotNet command-line options after the separator.
Do not compare Debug runs or runs conducted concurrently with material system
load.
