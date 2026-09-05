# Generic voxel values

The generic API accepts any `unmanaged` `T` whose runtime width is exactly 1, 2,
4, 8, or 16 bytes. This includes the fixed-width .NET binary integers through
`Int128`/`UInt128`, native integers, enums, and custom unmanaged structs. Variable-
width `BigInteger` is intentionally excluded by the 128-bit limit.

```csharp
var values = new UInt128[32 * 32 * 32];
var tree = new Octree<UInt128>(5, values);
UInt128 value = tree.Get(3, 7, 11);

[StructLayout(LayoutKind.Sequential, Pack = 1)]
readonly record struct Material(ulong Low, ulong High);

var materials = new Material[8 * 8 * 8];
var materialTree = new Octree<Material>(3, materials);
```

`Octree<T>`, `OctreeSpan<T>`, `VoxelFilter<T>`, `VoxelHit<T>`,
`DenseVoxelBlockSpan<T>`, `DenseVoxel<T>`, `OctreeChunk<T>`,
`OctreeNeighborhoodSpan<T>`, `OctreeNeighborhoodCache<T>`, and
`OctreeWorld<T>` mirror the corresponding `uint` APIs.

## Representation and performance

The size is computed once by the closed generic `VoxelType<T>` and validated when
an object or static operation is initialized. Other widths throw
`NotSupportedException`; a 3-byte or 12-byte struct is not widened to the next
integer size.

Each accepted `T` is reinterpreted at the codec boundary as exactly one `byte`,
`ushort`, `uint`, `ulong`, or 128-bit storage cell. The core codec then operates on
that exact-width type. Span builds and copies use `MemoryMarshal.Cast`; point reads
use `Unsafe.As`. These are reinterpretations, not numeric conversions or copies.
The JIT specializes value-type generic instantiations and folds the width dispatch.

All supported widths use native Morton reconstruction with reused uniform prefixes,
direct contiguous Morton decoding, and the shared axis-based layout permutation.
These paths preserve every voxel bit and require no linear scratch buffer:

```csharp
var morton = new UInt128[tree.Count];
tree.CopyTo(morton, DenseVoxelLayout.Morton);
tree.Build(morton, DenseVoxelLayout.Morton);
tree.AsSpan().TryCopyTo(morton, DenseVoxelLayout.Morton);
```

Aligned Morton block extraction uses the same contiguous decoder. Generic dense
fallback remains linear little-endian storage, so that case requires permutation
when the requested input or output layout is Morton.

The complete bit representation participates in equality, zero detection, masking,
and serialization. Custom structs should therefore use deterministic layout and
must not contain semantically irrelevant or uninitialized padding. Use explicit or
packed sequential layout when the default layout would contain padding.

## Serialization

Generic trees use format version 2. The three-byte header records the element width;
decoding through a different-width `T` is rejected. Uniform and tree values retain
the compact unsigned varint representation of their raw bits. Dense payloads use
fixed-width little-endian values.

The original non-generic `uint` API and version-1 encoding remain unchanged. Version
1 and version 2 are intentionally distinct; select `Octree`/`OctreeSpan` for stored
version-1 data and `Octree<T>`/`OctreeSpan<T>` for generic version-2 data.
