# Tedd.Octree

Compact, cache-friendly, read-only Octree implementation leveraging contiguous memory allocations and compact integer representations to significantly minimize memory footprint.

## Architectural Paradigms

The fundamental epistemological interface for developers navigating the `Tedd.Octree` structural framework relies on rigorous byte-level management and hierarchical recursive descent logic.

### Structural Framework

The primary operational paradigm of the project is built around storing multidimensional octree data within a linear, continuous segment of memory (`Memory<byte>`). This structural choice enforces memory locality and avoids object overhead from a typical managed memory graph model (e.g., recursive pointer chains of C# objects).

*   **Recursive Structure:** The hierarchy divides the structural volume systematically. At any level above level 0, a structural node acts as a pointer and describer of its 8 structural subnodes.
*   **Monotype Optimization:** If all 8 immediate subnodes exhibit identical types and are uniformly monotype themselves, the structural parent collapses into a monotype node. Monotype optimization drastically reduces storage footprint by bypassing the leaf descriptor requirement and recursive descent pointers entirely. Root-level monotypes can represent uniformly distributed volumes with near-zero overhead.
*   **Relative Jumping:** Instead of full multi-byte heap pointers, nodes reference their structural children using variable-size relative byte pointers (`RelativePos`), packing spatial jumps concisely.

### Theoretical Models & Operational Hypotheses

The internal framework architecture exhibits strategic intent towards future optimizations. Notably, in experimental implementations (`OctreeDev.cs`):

*   **SIMD Prefetching:** There is a documented hypothesis involving programmatic SIMD cache-line prefetching utilizing `System.Runtime.Intrinsics.X86.Sse.Prefetch0` on memory references during hierarchical traversal (`span[64]`). This hypothesis implies future framework enhancements optimized for latency reduction within extreme performance scenarios. Note that this feature is strictly an architectural hypothesis and does not represent standard operation in `Octree.cs`.

## Implementation Example: Data Retrieval

The `Get(int x, int y, int z)` function navigates the `Memory<byte>` segment via continuous bitwise adjustments and relative byte traversals.

```csharp
using System;
using Tedd.Octree;

public class Program
{
    public static void Main()
    {
        int levels = 4;
        int chunkSize = 1 << levels;

        // Build expects uncompressed voxel data (length = chunkSize^3), indexed as:
        // index = (x * chunkSize * chunkSize) + (y * chunkSize) + z
        var voxels = new UInt32[chunkSize * chunkSize * chunkSize];
        // TODO: populate voxels with your data

        var tree = new Octree(levels);
        tree.Build(voxels);
        // Extract value via spatial coordinates mapped to internal hierarchical bits
        int queryX = 1;
        int queryY = 2;
        int queryZ = 3;

        try
        {
            UInt32 nodeValue = tree.Get(queryX, queryY, queryZ);
            Console.WriteLine($"Extracted Node Data: {nodeValue}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Structural Retrieval Error: {ex.Message}");
        }
    }
}
```