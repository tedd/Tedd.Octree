# Tedd.Octree

Tedd.Octree is a compact, read-only octree for `UInt32` voxel data. Version 2 stores the tree in one versioned, contiguous byte encoding and exposes two views over it:

- `Octree`, a sealed owned wrapper.
- `OctreeSpan`, a `readonly ref struct` over caller-owned encoded memory.

For successful, valid-input operations, the caller-buffer build path and `OctreeSpan` lookup path perform no managed heap allocations. The owned build path allocates one final `byte[]` for the encoded tree. Error paths may allocate exception objects. Performance beyond these steady-state allocation contracts remains a hypothesis until measured on the target runtime and hardware.

## Installation

The package includes `net11.0`, `net10.0`, and `netstandard2.1` assets. .NET 11
is currently preview; the repository pins SDK `11.0.100-preview.7.26381.103`.
The .NET 10/11 builds use runtime-vectorized uniform-region detection, equality
searches/counting, and uninitialized result-array allocation when every byte is
subsequently written. The .NET Standard build uses portable scalar fallbacks.
The API and encoded byte format are identical across targets. .NET 11 also
benefits from its runtime's JIT improvements without a separate encoding.

```shell
dotnet add package Tedd.Octree --version 2.0.0
```

Or add the package reference directly:

```xml
<PackageReference Include="Tedd.Octree" Version="2.0.0" />
```

## Data model

An octree has `levels` in the inclusive range `0..9`:

```text
side  = 1 << levels
count = side * side * side
```

Level `0` represents one voxel. Level `9` represents a `512 x 512 x 512` volume.

The dense source must contain exactly `count` values in this order:

```text
index = (x * side * side) + (y * side) + z
```

`z` is therefore the fastest-changing coordinate, followed by `y`, then `x`. This ordering is part of the public input contract; do not pass a `z/y/x`-major buffer without reordering it.

## Owned tree

Use `Octree` when the tree must outlive an input buffer, cross an `async` boundary, or be retained in a heap object:

```csharp
using Tedd.Octree;

const int levels = 2;
const int side = 1 << levels;

var values = new uint[side * side * side];
values[(1 * side * side) + (2 * side) + 3] = 42;

var tree = new Octree(levels, values);

uint value = tree.Get(1, 2, 3);       // 42
uint same = tree[1, 2, 3];            // 42
bool inside = tree.Contains(1, 2, 3); // true

if (tree.TryGet(1, 2, 3, out uint found))
{
    // found == 42
}
```

The equivalent staged form is useful when the wrapper is created before its source is available:

```csharp
var tree = new Octree(levels);
tree.Build(values);
```

`Build` replaces the wrapper's owned encoding. The voxel data is read-only between builds.

## Caller-owned and span-backed tree

Use the static sizing/building API with `OctreeSpan` when the storage lifetime is controlled by the caller. The following path uses stack storage throughout:

```csharp
using Tedd.Octree;

const int levels = 2;
const int side = 1 << levels;

Span<uint> values = stackalloc uint[side * side * side];
values[(1 * side * side) + (2 * side) + 3] = 42;

int required = Octree.GetRequiredSize(values, levels);
Span<byte> storage = stackalloc byte[required];

if (!Octree.TryBuild(values, levels, storage, out int bytesWritten))
{
    throw new InvalidOperationException("The destination is too small.");
}

// Always exclude unused destination capacity.
var tree = new OctreeSpan(storage[..bytesWritten]);
uint value = tree[1, 2, 3];
```

`GetRequiredSize` calculates the exact encoded length for the supplied values. `Octree.Build(values, levels, destination)` is the throwing caller-buffer alternative and returns the number of bytes written. In both cases, construct the view from `destination[..bytesWritten]`, not the unsliced destination.

When one buffer will serve many volumes, `Octree.GetMaximumSize(levels)` returns a value-independent capacity. This avoids the sizing traversal; each build still reports its exact used length.

`OctreeSpan` does not copy or own its input. The backing bytes must remain alive and unchanged for the entire lifetime of the view. As a `ref struct`, the view is stack-bound: it cannot be boxed, stored in an ordinary class or struct field, captured by a closure, or used across `await` or `yield`.

For pooled or unmanaged buffers, apply the same lifetime rule and return or free the buffer only after the last lookup.

## Existing encoded data

Use `TryCreate` for untrusted or fallible span-backed input:

```csharp
ReadOnlySpan<byte> packet = GetPacket();

if (!OctreeSpan.TryCreate(packet, out OctreeSpan tree) || !tree.IsWellFormed())
{
    return; // Reject malformed or unsupported input.
}

uint value = tree.Get(0, 0, 0);
```

Use `FromEncoded` when an owned copy is required:

```csharp
byte[] packet = File.ReadAllBytes("volume.octree");
Octree tree = Octree.FromEncoded(packet);
```

`FromEncoded` validates the encoding and copies it. `AsSpan()` obtains a zero-copy, stack-bound view over an owned `Octree`. The `Data` property exposes encoded data as read-only memory (`ReadOnlyMemory<byte>` on `Octree`, `ReadOnlySpan<byte>` on `OctreeSpan`).

`CopyTo(Span<uint>)` and `TryCopyTo(Span<uint>)` decode the complete tree back to the same dense X/Y/Z-major layout accepted by the builders:

```csharp
var dense = new uint[tree.Count];
tree.CopyTo(dense);
```

## API summary

| API | Purpose | Allocation behavior |
| --- | --- | --- |
| `new Octree(levels)` | Create an unbuilt owned wrapper | Wrapper allocation only |
| `new Octree(levels, values)` | Build an owned tree | Wrapper plus one encoded `byte[]` |
| `Octree.Build(values)` | Build or rebuild an owned tree | One final encoded `byte[]` |
| `Octree.GetRequiredSize(values, levels)` | Calculate exact caller-buffer capacity | No managed allocation |
| `Octree.GetMaximumSize(levels)` | Calculate reusable worst-case capacity without inspecting values | No managed allocation |
| `Octree.Build(values, levels, destination)` | Build into a caller buffer; return bytes written | No managed allocation |
| `Octree.TryBuild(values, levels, destination, out bytesWritten)` | Non-throwing insufficient-capacity path | No managed allocation |
| `Octree.FromEncoded(data)` | Validate and copy an existing encoding | Wrapper plus one owned `byte[]` |
| `new OctreeSpan(data)` | Create a zero-copy view | No managed allocation |
| `OctreeSpan.TryCreate(data, out tree)` | Attempt to create a zero-copy view | No managed allocation |
| `Get` / `[x, y, z]` | Read a voxel | No managed allocation |
| `TryGet` | Read only when coordinates are valid | No managed allocation |
| `Contains` | Test coordinate bounds | No managed allocation |
| `CopyTo` / `TryCopyTo` | Decode into a caller-provided `Span<uint>` | No managed allocation |

Both representations expose `Levels`, `SideLength`, `Count`, `EncodedLength`, and `Data`. `Octree` also exposes `IsBuilt`; `OctreeSpan` exposes `IsValid` and `IsWellFormed()`. `Octree.MaxLevels` is `9`, and `Octree.FormatVersion` identifies the current byte-format version.

## Area and proximity queries

`Octree` and `OctreeSpan` provide allocation-free spatial queries:

| Method | Behavior |
| --- | --- |
| `Any(box, filter)` | Stops at the first matching voxel |
| `CountMatches(box, filter)` | Counts uniform regions without expanding them |
| `Query(box, filter, destination, out written)` | Writes `VoxelHit` results; returns false if more matches exist |
| `CopyRegionTo(box, destination)` | Decodes only a region into packed X/Y/Z-major values |
| `TryFindNearest(x, y, z, radius, filter, out hit)` | Finds the nearest matching voxel center within an inclusive Euclidean radius |

`VoxelBox` uses inclusive minima and exclusive maxima. Bounds must be inside the
chunk; an empty box produces no matches. Nearest-query centers must be inside
the chunk, and ties resolve by ascending X, then Y, then Z. Region traversal
prunes disjoint octants; nearest traversal also prunes by the closest possible
distance to an octant. Homogeneous regions are handled in bulk.
For tree-backed occupancy boxes of at most four cells, `Any` selects point
lookups to avoid general traversal setup for small support footprints.

`VoxelFilter.Any`, `NonZero`, `EqualTo(value)`, `NotEqualTo(value)`, and
`Masked(mask, expected)` avoid predicate delegates and boxing. A default filter
matches everything, including air. Use `NonZero` only when zero means empty in
the queried channel. For material-dependent collision, maintain a collision
channel or collision bits and query that classification.

```csharp
// Integer cells immediately below a 2-by-2 footprint; Y is the vertical axis.
var feet = new VoxelBox(x, y - 1, z, x + 2, y, z + 2);
bool hasSupport = collisionChannel.Any(feet, VoxelFilter.NonZero);

if (blocks.TryFindNearest(x, y, z, 8, VoxelFilter.EqualTo(waterBlockId), out var water))
{
    // water.X, water.Y, water.Z identify the nearest matching voxel center.
}

Span<VoxelHit> hits = stackalloc VoxelHit[64];
bool complete = blocks.Query(searchBox, VoxelFilter.NonZero, hits, out int written);
// hits[..written] is usable even when complete is false.
```

These queries operate on voxel cells. They do not implement continuous or swept
collision detection. A falling decision must also account for entity bounds,
velocity, collision rules, and neighboring chunks. Split cross-chunk boxes into
local boxes; the application defines whether an unloaded chunk is solid, empty,
or unknown. Query and regional decode destinations must not overlap encoded
bytes. Validate untrusted encoded data before spatial queries; malformed input
may leave partial destination output.

## Repeated checks around moving entities

`OctreeNeighborhoodCache` is a persistent, reusable dense window. Construct it
once with a power-of-two side length, or pass caller-owned `Memory<uint>`.
`OctreeNeighborhoodSpan` provides the same window over `Span<uint>` for stack,
pooled, or unmanaged storage without allocating a wrapper.

```csharp
var neighborhood = new OctreeNeighborhoodCache(side: 8); // 2 KiB voxel buffer.

// Before each query batch:
int refreshed = neighborhood.Update(collisionChannel, entityX, entityY, entityZ);
bool occupied = neighborhood.Get(entityX, entityY - 1, entityZ) != 0;
bool supported = neighborhood.Any(footprintInsideCachedBounds, VoxelFilter.NonZero);
```

Update centers and clamps the window to the chunk. It preserves overlapping cells
with ring indexing and fills only newly exposed slabs. A one-cell axis move of
an 8-cubed window refreshes 64 values; an unchanged window refreshes zero.
Teleports or source replacement refresh the whole window. Rebuilding an owned
`Octree` changes its encoding identity, which Update detects automatically.
Call Update before each query batch: Get/Any deliberately read the last cache
snapshot without consulting the tree.

For a borrowed `OctreeSpan`, pass a revision to
`Update(view, revision, x, y, z)` and change it on every in-place encoding update.
`Invalidate()` forces a refill; `Invalidate(changedBox)` does so only when the
changed region intersects the window. Neither cache form is thread-safe.
Its storage must remain exclusive and alive while in use. Cache bounds must fit
inside one chunk; inspect `Bounds` before issuing cached queries.

Cache capacity costs `4 * side^3` bytes plus wrapper metadata. A cache per worker
or active entity can help repeated local reads, but refresh overhead may exceed
the benefit for one short occupancy test. The benchmark suite includes this cost.
The .NET 10 support benchmark measured roughly 81 ns for a moving four-cell
`Any` check versus 806 ns when maintaining an 8-cubed cache for that check alone.
Repeated reads of compressed neighborhoods did benefit from caching; already
dense data generally did not. See the recorded benchmarks below.

## Channels, block updates, and dynamic entities

Use independent `Octree` instances as channels with the same levels and coordinate
layout. The existing UInt32 payload also supports packed bitfields; `Masked`
filters can select collision or material bits without unpacking every channel.

| Data | Suggested initial layout |
| --- | --- |
| Block/material ID | Independent channel |
| Collision class/flags | Independent channel or packed material bits |
| Orientation | Separate when sparsely populated or rarely queried |
| Fluid type and amount | Consider packing together when read and updated together |
| Light, temperature, simulation state | Independent channels by update/access pattern |
| Moving entities | Separate mutable spatial hash/grid or dynamic AABB index |

Independent channels preserve homogeneous regions when an unrelated attribute
varies, allow selective reads, and let a fluid update avoid rebuilding the block
channel. Packed attributes reduce traversals when all attributes are always
needed. `SpatialChannels` benchmarks both layouts using identical data; no layout
is universally optimal. Group publication of related channels at the application
level if readers require a consistent multi-channel snapshot.

This is a read-optimized chunk representation. Batch voxel edits in mutable dense
working storage, then rebuild and publish changed channel snapshots. Highly
active fluid/light chunks may be better kept dense during simulation. Do not
rebuild voxel octrees for entity movement: keep entity IDs, positions, velocity,
and bounds in a separate mutable index and query voxel collision channels for
terrain interaction.

## Validation and errors

- Levels outside `0..9` throw `ArgumentOutOfRangeException`.
- A dense source length other than `(1 << levels)^3` throws `ArgumentException`; partial volumes are not accepted.
- `Get` and the indexer require each coordinate to be in `[0, SideLength)`, otherwise they throw `ArgumentOutOfRangeException`. `TryGet` returns `false` for an out-of-range coordinate, and `Contains` reports whether the coordinate is in range.
- Calling `Get`, `CopyTo`, or `AsSpan` on an unbuilt owned `Octree` throws `InvalidOperationException`. Check `IsBuilt` when its state is uncertain.
- `TryBuild` returns `false` with `bytesWritten == 0` when the destination is too small. The throwing caller-buffer `Build` throws `ArgumentException`.
- `TryCopyTo` returns `false` when its destination contains fewer than `Count` values; `CopyTo` rejects insufficient capacity.
- `OctreeSpan.TryCreate` returns `false` for a malformed or unsupported header. The `OctreeSpan` constructor throws `FormatException` for the same header failure.
- Header validation alone does not traverse the encoded structure. Use `IsWellFormed()` before querying untrusted data; `Octree.FromEncoded` performs full structural and primitive-encoding validation and throws `FormatException` for malformed or unsupported input.

The `Try*` methods avoid exceptions only for their documented routine failure conditions. They do not suppress programmer errors such as invalid levels or an incorrectly sized dense source.

Source values and encoded build destinations must not overlap. Encoded input and dense decode destinations must likewise not overlap; `TryCopyTo` returns `false` and `CopyTo` throws `ArgumentException`. If `TryBuild` fails for insufficient capacity, bytes in the attempted destination are unspecified even though `bytesWritten` is zero.

## Memory and format characteristics

- The representation is one contiguous byte sequence rather than a graph of heap-allocated node objects.
- Homogeneous regions collapse into value leaves. The encoded size therefore depends on the spatial distribution of values, not only the voxel count.
- If the tree form would not beat direct storage, the encoder selects a dense payload. Consequently, `GetMaximumSize(levels)` is `2 + (4 * Count)` bytes and the encoded representation never exceeds that bound.
- Offsets are stored inside the encoding; there are no process-local pointers. Encoded data may be copied or persisted as bytes.
- Dense payloads are persisted in little-endian `UInt32` order; access remains portable across machine endianness.
- The encoding carries a format version. Package version `2.0.0` and byte-format `Octree.FormatVersion == 1` are distinct version domains.
- Consumers must treat `Data` and external backing storage as immutable while a tree is being read. Mutating encoded bytes invalidates the view and can corrupt subsequent lookups.

Version 2 encodings are not compatible with version 1. Validate persisted data before use and rebuild old data from its dense source.

## Concurrency

Concurrent reads are safe after a successful build when the encoded bytes are not mutated. Do not call `Octree.Build` concurrently with reads or another build. For `OctreeSpan`, synchronization and backing-buffer lifetime remain the caller's responsibility.

## Performance hypotheses

The benchmark suite is intended to test these hypotheses rather than presuppose them:

1. The caller-buffer builder and span-backed lookup path allocate zero managed bytes per operation.
2. The owned builder allocates one final array and avoids a temporary node graph.
3. The v2 post-order/backward-offset tree layout and direct dense fallback improve lookup latency relative to v1's forward-offset encoding for relevant data distributions.
4. Homogeneous and spatially clustered inputs compress substantially below dense `UInt32[]` storage.
5. Highly heterogeneous inputs select the dense fallback, trading two header bytes for direct indexing rather than tree traversal.

Results vary with data distribution, level count, JIT, runtime, CPU, and access pattern. Treat a claim as measured only when accompanied by current BenchmarkDotNet output for the relevant environment. Historical reports directly under `src/Tedd.Octree.Benchmark/Results` predate v2; the `2026-09-05` subdirectory records the modern v2 spatial, channel, and build measurements.

The recorded .NET 8 reference screening run confirmed 0 B caller-span builds and
lookups. Random level-5 lookup measured approximately 7.5 times faster than v1,
while compressed-tree lookup ranged from near parity to slower than v1. See
[`BENCHMARKS.md`](https://github.com/tedd/Tedd.Octree/blob/v2.0.0/src/Tedd.Octree.Benchmark/BENCHMARKS.md)
for the environment, method, and qualified results.

## Build, test, and benchmark

Run the test suite:

```shell
dotnet test src/Tedd.Octree.sln -c Release
```

Run all BenchmarkDotNet cases:

```shell
dotnet run -c Release -f net10.0 --project src/Tedd.Octree.Benchmark -- --filter '*'
```

The benchmark project retains the version 1 implementation as the internal `Tedd.Octree.Benchmark.Archive.V1.OctreeV1` baseline. It is test infrastructure, not supported public API. Benchmark names and categories label that implementation as `V1` so current, archived, span-backed, and dense-array results remain distinguishable.

Use a Release build, close competing workloads, and compare allocation columns as well as elapsed time. Commit benchmark artifacts only with their runtime, operating system, CPU, and BenchmarkDotNet metadata intact.

Use `-f net11.0` for preview-runtime measurements or `-f net8.0` to exercise the
portable .NET Standard asset. The test project runs on all three hosts. Spatial
benchmarks can be selected with `--filter '*Spatial*'`. BenchmarkDotNet
`0.16.0-preview.1` supplies .NET 11 support.

## Migrating from version 1

Version 2 is a major release because it replaces the public construction/storage contract and the encoded representation. Recompile consumers, rebuild persisted trees from dense voxel data, and choose explicitly between owned `Octree` and borrowed `OctreeSpan` lifetimes. The v1 implementation remains archived only in the benchmark project for controlled comparisons.

See [CHANGELOG.md](CHANGELOG.md) for the release summary.

## License

Tedd.Octree is licensed under the GNU Lesser General Public License version 2.1. See [LICENSE](LICENSE).
