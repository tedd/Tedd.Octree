# Dense blocks, channels, and streamed worlds

## Choose chunk depth separately from world depth

`Octree`/`OctreeSpan` encode a single channel with depth 0..9. `OctreeChunk`
groups independent encodings with one depth and channel count. `OctreeWorld`
adds sparse outer levels; its depth is configurable from the chunk depth through
20. No dense world-sized array or complete grid of chunk references is created.

| Configuration | Result |
| --- | --- |
| chunk levels 5 | 32 x 32 x 32 cells per chunk |
| world levels 10, chunk levels 5 | 1024-cubed world, up to 32 chunks per axis |
| world levels 16, chunk levels 5 | 65536-cubed world, with the same resident chunk size |
| world levels 20 | 1048576 cells per axis; `VoxelCount` is an Int64 |

One dense 1024-cubed UInt32 channel would require 4 GiB. The supported workflow
is to stream/edit bounded chunks, not allocate that world as a single buffer.
Dense block operations accept depths 0..9 and require an Int32-addressable output
span. Channel count multiplies dense capacity and is checked for overflow.
All coordinates are local nonnegative integer voxel coordinates within a world;
the application can map them to signed/global coordinates using a world origin.

## Linear and Morton indexing

Linear storage is X/Y/Z-major: `(x * side + y) * side + z`; Z changes fastest.
Morton storage uses the actual [Tedd.MortonEncoding NuGet package](https://www.nuget.org/packages/Tedd.MortonEncoding/1.0.1):
`Tedd.MortonEncoding.Encode((uint)x, (uint)y, (uint)z)`. X occupies bit 0, Y bit 1,
and Z bit 2 in each interleaved group. Thus `(1,0,0)` maps to 1 and `(0,0,1)` to 4.
Morton coordinates are relative to the extracted block, not the source world.

```csharp
using Tedd.Voxtree;

const int levels = 5;
const int side = 1 << levels;
const int count = side * side * side;
var linear = new uint[count];
linear[(1 * side + 2) * side + 3] = 42;
var morton = new uint[count];

DenseVoxel.Convert(linear, morton, levels,
    DenseVoxelLayout.Linear, DenseVoxelLayout.Morton);
int index = DenseVoxel.GetIndex(1, 2, 3, levels, DenseVoxelLayout.Morton);
uint value = morton[index]; // 42
```

`Convert` writes only the required prefix. Equal layouts allow overlapping spans;
different layouts reject overlap. `GetIndex` checks depth, layout, and coordinates.
Single-channel builders accept both orders and produce the same encoded format:

```csharp
var tree = new Octree(levels);
tree.Build(morton, DenseVoxelLayout.Morton);

// Allocate once and reuse; Build itself performs no managed allocation.
var encoded = new byte[Octree.GetMaximumSize(levels)];
int written = Octree.Build(morton, levels, encoded, DenseVoxelLayout.Morton);
var view = new OctreeSpan(encoded.AsSpan(0, written));

// Exact sizing is available when retaining minimal encoded capacity matters.
int exact = Octree.GetRequiredSize(morton, levels, DenseVoxelLayout.Morton);
bool built = Octree.TryBuild(morton, levels, encoded, out written, DenseVoxelLayout.Morton);
```

Native Morton builds traverse contiguous Morton subtrees without first converting
the input into a separate linear array. If compression selects dense encoding,
the persisted dense payload remains linear little-endian UInt32 data; input
layout is not a new serialized octree format.

## Extract a lower-level dense block

```csharp
var smallLinear = new uint[8 * 8 * 8];
var smallMorton = new uint[smallLinear.Length];
tree.CopyBlockTo(3, 2, 1, levels: 3, smallLinear);
view.CopyBlockTo(3, 2, 1, levels: 3, smallMorton, DenseVoxelLayout.Morton);

// Arbitrary rectangular extraction remains available in packed linear order.
var box = new VoxelBox(3, 2, 1, 11, 6, 9);
var rectangular = new uint[box.Count];
tree.CopyRegionTo(box, rectangular);
```

The block origin need not be aligned for extraction, but the entire block must
fit inside the source. Only the destination prefix is written. Output must not
overlap source encodings. Validate untrusted `OctreeSpan` encodings first;
malformed unchecked input may leave partial output.

## Build, inspect, and edit every channel

Dense multi-channel buffers are channel-major: all block IDs, then all
orientations, then all fluid types, then all fluid amounts. Each channel uses
the chosen linear or Morton layout independently. Channel meanings are defined
by the application; the library does not impose a material schema.

```csharp
const int channels = 4;
var denseValues = new uint[count * channels];
var dense = new DenseVoxelBlockSpan(denseValues, levels, channels, DenseVoxelLayout.Morton);
dense[0, 1, 2, 3] = 42; // Block ID
dense[1, 1, 2, 3] = 5;  // Orientation
dense[2, 4, 5, 6] = 1;  // Fluid type
dense[3, 4, 5, 6] = 12; // Fluid amount

OctreeChunk chunk = OctreeChunk.FromDense(levels, channels, denseValues, DenseVoxelLayout.Morton);
uint block = chunk.GetChannel(0).Get(1, 2, 3);
ReadOnlyMemory<byte> blockEncoding = chunk.GetChannelData(0);

// All channels are extracted into consecutive output slices.
chunk.CopyBlockTo(0, 0, 0, levels, denseValues, DenseVoxelLayout.Morton);
dense.GetChannelSpan(3).Fill(0); // Large fluid update in the mutable working buffer.

// Rebuild only the changed channel; unchanged encodings are shared.
OctreeChunk changed = chunk.WithDenseChannel(3, dense.GetChannelSpan(3), DenseVoxelLayout.Morton);
// Or rebuild all channels after a multi-channel edit.
OctreeChunk rebuilt = OctreeChunk.FromDense(levels, channels, denseValues, DenseVoxelLayout.Morton);
```

`DenseVoxelBlockSpan` is a mutable ref struct: its storage remains caller-owned
and exclusive while editing. `OctreeChunk` snapshots are immutable. Each dense
factory allocates encoded output arrays plus chunk/descriptors; it does not claim
to be allocation-free. The caller-buffer `Octree.Build` path remains available
for pooled/unmanaged encoded payloads, which can then be attached without copying:

```csharp
// These memories must remain alive and immutable while the chunk is retained.
var encodings = new ReadOnlyMemory<byte>[channels];
for (int c = 0; c < channels; c++)
{
    var bytes = new byte[Octree.GetMaximumSize(levels)]; // Provision or rent storage here.
    int used = Octree.Build(dense.GetChannelSpan(c), levels, bytes, DenseVoxelLayout.Morton);
    encodings[c] = bytes.AsMemory(0, used);
}
var borrowed = new OctreeChunk(levels, encodings);
```

The constructor validates all channels and copies only memory descriptors.
Borrowing does not transfer ownership of a pool rental or unmanaged allocation.
Do not return/free it while any chunk, world, view, or cache still references it.

## Stream a 1024-cubed world

```csharp
var world = new OctreeWorld(
    levels: 10, chunkLevels: 5, channelCount: 4,
    chunkCapacity: 256, branchCapacity: 2048);

// Default state is UNLOADED, not air.
var area = new VoxelBox(0, 0, 0, 64, 64, 64);
bool knownAir = world.IsKnownEmpty(area, channel: 0); // false initially

// After generation/storage has established that this aligned region is empty:
if (!world.TrySetEmptyRegion(0, 0, 0, levels: 6))
    throw new InvalidOperationException("Increase branch capacity.");

// Load uses CHUNK coordinates; queries use VOXEL coordinates.
world.LoadChunk(chunkX: 0, chunkY: 0, chunkZ: 0, chunk);
uint material = world.Get(channel: 0, x: 1, y: 2, z: 3);

var feet = new VoxelBox(0, 1, 2, 2, 2, 4);
if (world.TryAny(feet, channel: 0, VoxelFilter.NonZero, out bool supported))
{
    // The occupancy decision is conclusive. Apply the application's collision rules.
}
else
{
    // No known support was found and unloaded data remains. Request loading or defer.
}

// Publish a rebuilt chunk. Existing references to the previous snapshot remain valid.
world.LoadChunk(0, 0, 0, changed);
```

`TryGet` returns false for unloaded/out-of-bounds cells. Known-empty cells return
zero. `Get` throws for either condition. `TryAny` can conclusively return a known
match even if other cells are unloaded; a negative result is conclusive only
when all relevant cells are known. `Any` throws if it cannot decide.
`TryCountMatches` returns an Int64 count and false if unknown cells were excluded.
`IsKnownEmpty` never equates unloaded with air.

```csharp
bool complete = world.TryCountMatches(area, 0, VoxelFilter.NonZero, out long solidCount);
OctreeWorldRegion region = world.GetRegion(100, 100, 100);
// region.Kind: Unloaded, Empty, or Loaded; region.Levels describes the stored extent.

var allAir = new OctreeWorld(20, 5, 4, chunkCapacity: 0, initiallyEmpty: true);
bool empty = allAir.IsKnownEmpty(new VoxelBox(0, 0, 0, 1 << 20, 1 << 20, 1 << 20));
// One known-empty root, no retained chunks, no live branches.
```

`initiallyEmpty: true` is an assertion that every channel is already known zero.
It is not an unloaded-data policy. An empty chunk loaded with `OctreeChunk.Empty`
is represented by metadata only; no payload slot is retained. Adjacent empty
siblings collapse, and unloaded siblings collapse separately. Empty means zero
in **all** channels for structural collapse; channel-specific zero checks also
work on resident chunks.

## Cross-chunk extraction and bulk modification

```csharp
// All intersected cells must be known before any output is written.
var window = new uint[count * channels];
world.CopyBlockTo(0, 0, 0, levels: 5, window, DenseVoxelLayout.Morton);
var oneChannel = new uint[count];
world.CopyChannelBlockTo(0, 0, 0, 0, levels: 5, oneChannel);
var narrow = new VoxelBox(0, 0, 0, 16, 8, 16);
var narrowValues = new uint[narrow.Count];
world.CopyRegionTo(channel: 0, narrow, narrowValues);
```

Origins need not coincide with chunk boundaries; extraction traverses only
intersecting stored regions. To modify a large area, iterate its affected chunk
coordinates, extract each complete chunk into reusable dense working storage,
edit it, and publish a replacement with `LoadChunk`. This preserves voxels outside
the edit area and avoids rebuilding the outer world or unrelated channels.
Cross-chunk extraction is one operation; replacement of multiple chunks is a
sequence of publications, not a multi-chunk transaction. Synchronize that batch
at the application level when readers require a consistent snapshot.

## Save, unload, reload

```csharp
if (world.TryGetChunk(0, 0, 0, out OctreeChunk? resident))
{
    byte[] packet = new byte[resident!.SerializedLength];
    int used = resident.CopyEncodedTo(packet);
    File.WriteAllBytes("chunk-0-0-0.octree", packet); // I/O/allocation is outside the hot path.

    bool evicted = world.TryUnloadRegion(0, 0, 0, levels: 5);
    // Eviction makes this region unknown, not known air.

    byte[] loadedPacket = File.ReadAllBytes("chunk-0-0-0.octree");
    OctreeChunk loaded = OctreeChunk.FromEncoded(loadedPacket.AsMemory(0, used));
    world.LoadChunk(0, 0, 0, loaded);
}
```

Chunk packets contain a version, depth, channel count, and length-delimited
octree encodings. Integers use little-endian order. Loading validates every
channel, allocates only descriptors/the chunk wrapper, and borrows packet memory.
Packets must be exact slices. Malformed/truncated/trailing data is rejected.
`CopyEncodedTo` writes into a caller-provided buffer without allocating and rejects
overlap before writing. Packets are Int32-sized; larger channel sets can save
individual `GetChannelData` payloads in separate files instead.

Save before eviction if modifications must persist. Eviction releases the world's
references; it does not delete files, free caller-owned native memory, or return
buffers to a pool. References held by other worlds, snapshots, and caches remain.
`TryGetChunk` is false for both empty and unloaded regions; use `GetRegion` to
distinguish them. Known-empty regions need only a manifest marker, not a chunk file.

## Encapsulate lower levels in a larger world

```csharp
var smallWorld = new OctreeWorld(10, 5, 4, 256, branchCapacity: 2048, initiallyEmpty: true);
smallWorld.LoadChunk(0, 0, 0, chunk);
var largerWorld = new OctreeWorld(16, 5, 4, 256, branchCapacity: 4096);

// A tree with B live eight-child branches has 1 + 7*B stored leaf regions.
var manifest = new OctreeWorldRegion[1 + 7 * smallWorld.BranchCount];
var smallBounds = new VoxelBox(0, 0, 0, 1024, 1024, 1024);
if (!smallWorld.QueryRegions(smallBounds, manifest, out int regionCount))
    throw new InvalidOperationException("Manifest capacity is insufficient.");
foreach (var entry in manifest.AsSpan(0, regionCount))
{
    if (!largerWorld.TryLoadRegion(entry, offsetX: 2048, offsetY: 0, offsetZ: 0))
        throw new InvalidOperationException("Destination world capacity is insufficient.");
}
```

Region enumeration preserves large empty/unloaded extents; it does not expand
them into individual chunks. Returned regions are not clipped and may extend
beyond the query box. Truncated output remains usable and `written` reports its
count; use separate output buffers if retaining an earlier manifest.

Imports retain chunk encodings without copying them. Translation must preserve
each region's alignment. Loaded chunks must match the destination chunk depth
and channel count; world depths can differ. Each import is atomic, but the full
loop is not transactional. For disk manifests, store world/chunk depths, channel
schema, each region's origin/depth/kind, and a packet key for loaded entries.
Reconstruct a descriptor with `new OctreeWorldRegion(x, y, z, levels, kind, chunk)`
and pass it to `TryLoadRegion`. The application owns manifest format and I/O.

## Capacity, memory, concurrency, and caching

```csharp
const int branches = 2048, slots = 256;
var workspace = new int[OctreeWorld.GetRequiredWorkspaceLength(branches, slots)];
var chunkSlots = new OctreeChunk?[slots];
var borrowedWorld = new OctreeWorld(10, 5, 4, workspace, chunkSlots, branches);
// Construction clears both caller-owned memories; keep them exclusive afterward.

Console.WriteLine($"{borrowedWorld.BranchCount}/{borrowedWorld.BranchCapacity} branches");
Console.WriteLine($"{borrowedWorld.ResidentChunkCount}/{borrowedWorld.ChunkCapacity} payloads");

var cache = new OctreeNeighborhoodCache(8);
cache.Update(chunk.GetChannelData(0), revision: world.Revision, x: 8, y: 8, z: 8);
uint cached = cache.Get(8, 8, 8); // Chunk-local coordinates.
```

Provisioned index storage costs approximately `32 * branchCapacity +
(4 + IntPtr.Size) * chunkCapacity` bytes, excluding array/object headers and
encoded chunk payloads. Arena nodes and payload slots are recycled without
allocation. The default branch capacity reserves one worst-case path per payload
slot. Mixed empty/unloaded metadata can also consume branches, so provide extra
capacity for fragmented streaming states. Capacity exhaustion returns false from
`TryLoadChunk`, `TryLoadRegion`, `TrySetEmptyRegion`, or `TryUnloadRegion` without
changing state. `LoadChunk` throws instead. Capacity does not grow implicitly.

Every successful change advances `Revision`; loading the identical immutable
chunk again does not. Borrowed-channel caches detect new memory identity and/or
revision, but must be updated before each batch. A global world revision is
conservative and may invalidate a cache after an unrelated edit; applications
can track per-chunk revisions. Chunk-local caches must not be queried outside
their bounds. Use world queries/extraction or multiple chunk caches at borders.

World operations acquire shared read or exclusive write locks automatically.
Mutable neighborhood caches and dense working buffers remain exclusive to their
worker. A retained chunk snapshot can be read without world locks, including
after replacement or eviction, provided its borrowed backing storage is still
alive and immutable. A world lock does not protect external buffer mutation.
Use a read batch when pairing a chunk lookup with `Revision` or when sizing a
manifest from `BranchCount` before enumeration.

## Allocation contracts

| Operation | Managed allocation |
| --- | --- |
| Dense conversion, indexing, block extraction | 0 after caller buffers exist |
| Caller-buffer linear/Morton build | 0 |
| `OctreeChunk.FromDense` | Final channel arrays, descriptors, wrapper |
| `WithDenseChannel` | One final channel array, descriptors, wrapper |
| Chunk constructor / `FromEncoded` | Descriptors and wrapper; payloads borrowed |
| `CopyEncodedTo` | 0 |
| World construction | Workspace/slot arrays, wrapper, and synchronization object; caller-memory overload omits arrays |
| Load/evict/import, point/area/count queries, region enumeration | 0 after provisioning and lock warmup on each worker |
| World dense extraction | 0 after destination provisioning and lock warmup |
| Read/write batch scopes | 0 after lock warmup; no per-scope heap object |

First use on a worker may allocate lock bookkeeping; contention may allocate wait
handles. Dispose worlds after stopping their workers to release lock resources.
Exception paths may allocate. BenchmarkDotNet suites `BulkBlocks` and `BulkWorld`
test steady-state paths and exclude setup allocations. See the benchmark guide
for measured results and the distinction between dense-buffer cost, encoded
payload cost, index capacity, and resident working set.


## Threading and deferred updates

The world uses one reader/writer lock per instance. Concurrent readers share the
lock; load, replace, empty, and unload operations acquire it exclusively. The
complete region traversal or dense copy stays under one read lock, including
validation and all channels. Counts and `Revision` are also synchronized.

```csharp
using (world.BeginReadBatch())
{
    ulong revision = world.Revision;
    bool resident = world.TryGetChunk(0, 0, 0, out var snapshot);
    // Use revision and snapshot together here, or retain both for a worker cache.
}

// Encode independently owned, stable dense input before taking the world lock.
var replacement = OctreeChunk.FromDense(5, 4, denseValues);
using (world.BeginWriteBatch())
{
    world.LoadChunk(0, 0, 0, replacement);
    world.TrySetEmptyRegion(32, 0, 0, 5);
}
```

Calls inside a batch reuse its held lock. Read batches may nest in read or write
batches; write batches may nest in write batches. An ordinary read batch cannot
upgrade to write: it throws `LockRecursionException` immediately. This follows
[ReaderWriterLockSlim's threading rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-threading-readerwriterlockslim).
Use a write batch from the outset for atomic read-modify-write operations.
Scopes must be disposed in reverse acquisition order on their creating thread,
and must not be copied or cross `await`. Acquire multiple worlds in a consistent
application-defined order if an operation needs several world locks.

Batch writes become observable to other threads on disposal. Batches do not
provide rollback or reserve capacity for future calls: if the third load fails,
the first two remain applied. Every actual change advances `Revision`, even
inside a batch; no-op loads and failed capacity checks do not. Capturing several
chunks in a short read batch then querying those immutable snapshots outside it
can reduce writer blocking when a stable historical view is sufficient.

### Should dirty blocks be queued?

Coalescing **dense voxel edits before rebuilding a changed channel** is the
likely useful optimization for this implementation. A resident nonempty chunk
replacement already performs an outer-path lookup, swaps its immutable payload
reference, and advances the revision. It does not rebuild the outer tree. Branch
splitting/collapse occurs when residency or empty/unloaded state changes. Merely
queuing already-encoded replacements primarily amortizes locking and can discard
superseded versions; it does not save encoding that already happened.

A suitable application workflow is:

1. Give a worker exclusive ownership of each mutable dirty chunk, and coalesce
   repeated edits by chunk/channel. Keep actively simulated chunks dense.
2. At a simulation tick or bounded flush, encode each changed channel once using
   `WithDenseChannel`, or build a complete `OctreeChunk`. Encode outside world locks
   from a stable buffer or private snapshot.
3. Publish completed chunks in a short `BeginWriteBatch()` scope. Check the
   expected previous chunk identity or an application-owned generation under
   that same write lock before replacing it. Retry or merge a stale result;
   otherwise a slow worker can overwrite newer edits or reload an evicted chunk.
4. Bound pending work and publication batch size. Define backpressure, flush on
   save/shutdown, and handling of fixed world-capacity failures.

A dedicated maintenance thread is optional. The simulation loop can flush at
explicit ticks, or existing workers can encode and a designated owner can publish.
Add background maintenance when measured rebuild time or publication latency
justifies it. Readers then see the last published state until a flush. Immediate
read-after-edit semantics would require consulting the dirty working state as
well, complicating cross-chunk queries and cache invalidation. No background
queue or delayed visibility is enabled by the library.

`WorldSynchronization` benchmarks individual versus batched reads/writes and
retained-chunk reads. They measure uncontended costs; use representative worker
counts, query lengths, and write rates to assess contention and tail latency.
