# Changelog

All notable changes to Tedd.Voxtree are documented here.

## [2.0.0] - 2026-09-05

### Breaking changes

- Replaced the version 1 construction contract and byte format with a versioned, span-oriented representation.
- Sealed the owned `Octree` wrapper and introduced the stack-bound `readonly ref struct OctreeSpan` for zero-copy access to caller-owned bytes.
- Standardized dense source ordering as `(x * side * side) + (y * side) + z`.
- Constrained levels to the inclusive range `0..9`; level `0` represents one voxel.
- Changed persisted encoding. Version 1 encoded data must be rebuilt from its dense source and is not accepted as version 2 data.
- Defined explicit validation for source lengths, coordinates, build state, destination capacity, and malformed encodings.

### Added

- Effectively unbounded `WorldEntity`/`WorldEntity<T>` coordinate layers backed by
  chunk dictionaries, signed 64-bit coordinates, bit-shift routing for mandatory
  power-of-two chunk sides, and hash-indexed ten-entry recent-chunk caches.
- Exact-width generic voxel APIs for arbitrary `IBinaryInteger<TSelf>` values and
  unmanaged custom structs of 1, 2, 4, 8, or 16 bytes, including chunks,
  neighborhoods, spatial queries, dense layouts, and sparse worlds.
- Linear and Tedd.MortonEncoding-ordered dense import/export, unaligned lower-level block extraction, and mutable multi-channel dense span views.
- Immutable multi-channel chunk snapshots with selective channel rebuilds and versioned, validated chunk packets for save/load.
- Sparse fixed-capacity outer octrees through depth 20, independently configurable chunk depths, explicit unloaded/known-air states, recyclable branches/payload slots, and zero-allocation streaming operations.
- Cross-chunk area queries/counts and dense extraction, plus region manifests and translated lower-world imports into larger worlds.
- Bulk-editing, persistence, ownership, capacity, and streaming examples covering all added APIs.
- Targets for .NET 11 preview, .NET 10, and .NET Standard 2.1 with equivalent APIs and encoding; cross-target tests run on .NET 11, .NET 10, and .NET 8.
- Pruned box occupancy/counting, bounded voxel collection, regional decoding, and nearest matching voxel searches.
- Delegate-free exact, nonzero, and masked value filters.
- Persistent and caller-span sliding neighborhood caches with ring indexing, slab refreshes, source-identity/revision invalidation, and explicit regional invalidation.
- Voxel-world guidance covering channels, support checks, chunk boundaries, mutable simulation data, and independent moving-entity indexes.
- Benchmarks for spatial queries, cache movement/invalidation, and packed versus independent attribute channels.
- `Octree.GetRequiredSize(ReadOnlySpan<uint>, int)` for exact destination sizing.
- `Octree.GetMaximumSize(int)` for reusable value-independent destination sizing.
- Allocation-free caller-buffer `Octree.Build` and `Octree.TryBuild` overloads.
- Owned construction from dense data and `Octree.FromEncoded(ReadOnlySpan<byte>)` for validated copying.
- `OctreeSpan(ReadOnlySpan<byte>)`, `OctreeSpan.TryCreate`, `IsValid`, and `IsWellFormed()` for span-backed encoded data.
- `Get`, `TryGet`, `Contains`, and `[x, y, z]` lookup APIs on owned and span-backed trees.
- `CopyTo`, `TryCopyTo`, `AsSpan`, and encoded-data metadata.
- `Levels`, `SideLength`, `Count`, `EncodedLength`, `MaxLevels`, and `FormatVersion` metadata.
- Expanded tests for boundary levels, dense indexing order, homogeneous and heterogeneous volumes, malformed input, undersized destinations, copy behavior, and owned/span parity.
- BenchmarkDotNet coverage for building, lookup, and managed allocations across representative data distributions.

### Performance design

- Morton reconstruction reuses scanned uniform prefixes across tree depths; full and aligned-subtree Morton decoding writes contiguous output intervals without per-voxel coordinate conversion.
- Added explicit dense-layout overloads for `CopyTo` and `TryCopyTo`; linear/Morton permutation reuses axis encodings from Tedd.MortonEncoding.
- Archived the pre-optimization conversion code with paired BenchmarkDotNet cases for repeatable regression comparisons.
- Native Morton builds avoid a conversion scratch buffer; uniform blocks and aligned homogeneous Morton octants use contiguous fills during extraction.
- Streaming branches and resident slots use fixed-capacity recyclable arenas; known-empty outer regions collapse without retaining dense buffers or per-chunk payloads.
- .NET 10/11 use SIMD-enabled span searches and counting, early uniform-region detection, and uninitialized result-array allocation; .NET Standard retains portable fallbacks.
- Small occupancy footprints use a measured point-query fast path; larger queries prune octants and process homogeneous regions in bulk.
- Caller-buffer building and span-backed lookup perform zero managed heap allocations.
- Owned building allocates one final encoded `byte[]` and does not retain a temporary object graph.
- Owned building reuses the sizing pass's terminal storage decision, avoiding a second source traversal for uniform and dense encodings.
- Encodings automatically fall back to a direct little-endian dense payload when the tree form would not be smaller, capping capacity at `2 + (4 * Count)` bytes.
- Owned lookup dispatches directly to the validated encoding, keeping the successful hot path allocation-free and avoiding repeated view construction.
- The benchmark project archives the version 1 implementation as `Tedd.Voxtree.Benchmark.Archive.V1.OctreeV1` and uses it as the labeled `V1` baseline.
- Throughput, latency, locality, and compression advantages remain benchmark hypotheses until supported by current results for a specified runtime and machine.

### Concurrency

- World reads use shared locks; mutations use exclusive locks. Complete queries/copies and mutable metadata are synchronized.
- Added allocation-free `BeginReadBatch()` / `BeginWriteBatch()` scopes after lock warmup, with compatible nesting, thread affinity, and exception-safe release. Batches provide exclusion without rollback; read-to-write upgrades are rejected.
- Worlds implement `IDisposable` to release synchronization resources after all workers have stopped.
- Owned `Octree` rebuilds atomically publish immutable encodings; concurrent rebuild/read and rebuild/rebuild operations are supported with stable input buffers. Captured spans retain their original encoding.
- Immutable chunks remain usable after world replacement/eviction; borrowed backing-memory lifetime remains the caller's responsibility. Mutable dense views and neighborhood caches require worker-exclusive storage.
- Added concurrency stress tests, batching benchmarks, and dirty-channel coalescing/publication guidance.

### License

- Distribution remains under the repository's GNU Lesser General Public License version 2.1.
