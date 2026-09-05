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

Native Morton builds scan contiguous octant slices and reuse each scanned uniform
prefix in descendants, avoiding repeated scans at successive depths. Each build
pass takes O(N) work and O(depth) stack space, without a layout-conversion buffer
or coordinate encoding per voxel. Equal-value runs collapse only where they cover
complete aligned octants; an octree is not arbitrary run-length encoding.
The persisted child order remains Z-first, with a three-bit permutation to the
Morton package's X-first order, so linear and Morton inputs produce identical bytes.

Full-volume decoding accepts the requested layout directly:

```csharp
view.CopyTo(morton, DenseVoxelLayout.Morton);
bool copied = tree.TryCopyTo(morton, DenseVoxelLayout.Morton);
```

Morton tree decoding writes forward through contiguous output intervals and fills
each uniform octant in bulk. Aligned `CopyBlockTo` subtrees use the same decoder;
unaligned blocks retain clipped spatial traversal. `CopyTo`/`TryCopyTo` validate
the complete encoding before writing, while block queries require prior validation
of untrusted input. Rebuilding or decoding a tree in Morton order does not require
an intermediate linear array.

When a layout change is necessary, `DenseVoxel.Convert` uses Tedd.MortonEncoding
once per axis coordinate and combines the interleaved bits for each voxel, using
at most 2 KiB of stack storage. If compression selects dense encoding,
the persisted dense payload remains linear little-endian UInt32 data; input
layout is not a new serialized octree format. This fallback still requires a
layout permutation when importing or exporting Morton data.

The generic `Octree<T>` and `OctreeSpan<T>` APIs use the same strategy for all
supported storage widths; see [generic voxel values](GENERIC_VALUES.md).

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

For repeated edits to the same chunk, retain a hot editor instead of decoding and
rebuilding for every change:

```csharp
HotOctreeChunk hot = chunk.MarkHot();
hot[0, 1, 2, 3] = 43;
hot.GetChannelSpan(3).Clear();
OctreeChunk snapshot = hot.UnmarkHot(); // Encodes every channel once and closes the editor.
```

The hot buffer is channel-major and always Morton ordered. It is exclusively
owned mutable storage; `OctreeChunk` remains the immutable publication format.

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
coordinates and call `MarkChunkHot` for each resident or known-empty chunk. Edit
the returned Morton buffers repeatedly, then publish each with `CommitHotChunk`
or `UnmarkChunkHot`. This preserves voxels outside the edit area and avoids
rebuilding the outer world between edits.
Cross-chunk extraction is one operation; replacement of multiple chunks is a
sequence of publications, not a multi-chunk transaction. Synchronize that batch
with `BeginWriteBatch` when readers require one consistent publication point.

## Save, unload, reload

`WorldEntity` and `WorldEntity<T>` provide a disk-backed residency layer:

```csharp
var storage = new ChunkStorageOptions("world/chunks")
{
    Compression = ChunkCompression.Zstandard,
    CompressionLevel = CompressionLevel.Fastest
};
var global = new WorldEntity(chunkSize: 32, channelCount: 4, storage,
    maxResidentBytes: 512L * 1024 * 1024);

global.SetChunk(-1, 0, 2, chunk); // Resident and dirty.
global.SaveAllChunks();           // Writes dirty residents; clean chunks are skipped.
global.UnloadChunk(-1, 0, 2);    // Flushes if dirty, then unloads.
uint loaded = global.Get(0, -1, 0, 64); // Reloads automatically.

// Missing storage means known zero, not an error. The clean implicit chunk is
// never written unless it is subsequently replaced or hot-edited and committed.
uint air = global.Get(0, 3200, 0, 0);

global.MaxResidentBytes = 256L * 1024 * 1024;
int evicted = global.TrimToMemoryTarget();
```

Chunk paths are deterministically sharded into two coordinate-derived directory
levels, avoiding a single unbounded directory. Each file records its coordinate,
LOD level, compression format, compressed and raw lengths, and CRC32. Raw
lengths are bounded
against the configured chunk schema before decompression. The payload is the same
versioned chunk packet described below. Saves write a same-directory temporary file
and atomically move or replace it, so an interrupted encoder does not truncate the
previous file.

`ChunkCompression` supports `Direct`, `Brotli`, `Deflate`, `GZip`, `ZLib`, and
`Zstandard`. Zstandard is the default and uses the native .NET 11 implementation.
It is deliberately reported as unsupported on earlier targets instead of silently
writing another format. The .NET Standard 2.1 asset similarly excludes ZLib;
Brotli, Deflate, GZip, and direct packets remain available. A file identifies its
own format, so changing `ChunkStorageOptions.Compression` affects subsequent saves
without invalidating older files. Replacing a world's `StorageOptions` marks every
persisted resident dirty so subsequent trimming first persists them under the new root.

Every resident replacement and committed hot edit is dirty. `SaveAllChunks`,
`Clear`, explicit unload, and LRU eviction persist dirty data before discarding
it. Clean disk-loaded chunks and clean implicit-zero chunks are skipped. An
explicit `SaveChunk` can persist an implicit-zero chunk when a physical record is
required. If a flush fails, the affected chunk remains resident.

Every world lookup updates a monotonic `LastAccessSequence`; storage maintenance
and metadata inspection do not. `EstimatedResidentBytes` totals serialized packet
sizes. This intentionally stable metric excludes CLR object headers, dictionary
capacity, shared backing-memory attribution, hot-editor buffers, and unrelated
process memory. `MaxResidentBytes` is therefore a soft policy target, not an exact
heap limit. Assigning it performs no I/O. `TrimToMemoryTarget` saves dirty LRU
chunks before eviction; loads and implicit-zero materialization trim older chunks
automatically but protect the requested chunk, permitting one oversized chunk to
exceed the target. I/O and
format failures throw. A configured world treats an absent file as a clean,
all-zero resident chunk.

`WorldEntity` storage calls are synchronous and the type remains non-thread-safe.
Coordinate external workers before loading, saving, trimming, or accessing it.
An active hot editor retains an immutable source reference; eviction makes a later
hot commit stale rather than restoring an evicted chunk.

### Persist caller-generated LOD chunks

LOD layers retain the same `ChunkSize` samples per axis while representing
increasing power-of-two coverage in base-world coordinates:

```csharp
// For ChunkSize 32: LOD 0/1/2 coverage is 32/64/128 base voxels per axis.
ChunkAddress address = global.GetChunkAddress(lodLevel: 2, x: -1, y: 7, z: 128);
global.SetChunk(address, externallyGeneratedLodChunk); // Dirty LOD publication.
global.SaveChunk(address);

uint sample = global.GetLod(lodLevel: 2, channel: 0, x: -1, y: 7, z: 128);
```

The address shift is `ChunkShift + LodLevel`; the local sample coordinate uses
the next `ChunkShift` bits after discarding the LOD bits. Signed coordinates use
arithmetic shifts. `ResidentChunks` exposes all loaded `ChunkAddress` entries,
while the compatibility `Chunks` view exposes only LOD 0. LOD files are stored
under independent `lod-N` roots and record their level in the checked file header.
Base-layer files written by the preceding header version remain loadable.

The library does not calculate, select, invalidate, or reconcile LOD values.
Applications must generate each `OctreeChunk`, decide when it is stale, and
publish replacements through `SetChunk` or the address-aware hot-chunk APIs.

The bounded `OctreeWorld` retains its caller-controlled packet workflow:

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
Hot chunks, mutable neighborhood caches, and dense working buffers remain
exclusive to their worker. A retained chunk snapshot can be read without world locks, including
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
| `MarkHot` / `MarkChunkHot` | One dense Morton array and editor wrapper |
| Hot-chunk commit | Final channel arrays, descriptors, immutable chunk wrapper |
| Chunk constructor / `FromEncoded` | Descriptors and wrapper; payloads borrowed |
| `CopyEncodedTo` | 0 |
| WorldEntity save/load | Packet, compression, and file buffers plus stream objects |
| WorldEntity trim | Ordered candidate list; dirty eviction additionally has save costs |
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

### Hot chunks and deferred publication

`HotOctreeChunk` directly supports coalescing dense voxel edits before rebuilding.
`MarkChunkHot` captures the current immutable chunk under the read lock and decodes
an exclusively owned, channel-major Morton array outside it. The resident snapshot
remains visible to readers while the editor is modified. A bounded world can also
promote a known-empty chunk; an unloaded chunk cannot be promoted.

A suitable application workflow is:

1. Give one worker exclusive ownership of each `HotOctreeChunk` and coalesce
   repeated edits by chunk/channel.
2. At a simulation tick or bounded flush, call `TryCommitHotChunk`. Without an
   enclosing write batch, it encodes once before acquiring the exclusive world
   lock, verifies that the source snapshot remains current, and publishes it.
   A stale source or insufficient world capacity returns false; a still-mutable
   editor retains its modifications for retry or merge.
3. To publish several prepared chunks together, call `Commit()` on each editor
   outside the lock, then call the world's `TryCommitHotChunk` methods inside a
   short `BeginWriteBatch()` scope. The source-identity checks still apply.
4. Bound pending work and publication batch size. Define backpressure, flush on
   save/shutdown, and handling of fixed world-capacity failures.

A dedicated maintenance thread is optional. The simulation loop can flush at
explicit ticks, or existing workers can encode and a designated owner can publish.
Add background maintenance when measured rebuild time or publication latency
justifies it. Readers see the last published immutable state until a flush;
the owning worker reads pending values through the hot editor. No background
queue or automatic flush is enabled by the library.

`WorldSynchronization` benchmarks individual versus batched reads/writes and
retained-chunk reads. They measure uncontended costs; use representative worker
counts, query lengths, and write rates to assess contention and tail latency.
