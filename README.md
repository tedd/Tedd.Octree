# Tedd.Octree

Compact, cache-friendly, read-only Octree implementation leveraging contiguous memory allocations and compact integer representations to significantly minimize memory footprint.

## Architectural Paradigms

The `Tedd.Octree` implementation stores the tree in a single contiguous byte buffer and traverses it by decoding per-level descriptors and relative offsets.

### Structural Framework

The primary operational paradigm of the project is built around storing multidimensional octree data within a linear, continuous segment of memory (`Memory<byte>`). This structural choice enforces memory locality and avoids object overhead from a typical managed memory graph model (e.g., recursive pointer chains of C# objects).

*   **Recursive Structure:** The hierarchy divides the structural volume systematically. At any level above level 0, a structural node acts as a pointer and describer of its 8 structural subnodes.
*   **Monotype Optimization:** If all 8 immediate subnodes exhibit identical types and are uniformly monotype themselves, the structural parent collapses into a monotype node. Monotype optimization drastically reduces storage footprint by bypassing the leaf descriptor requirement and recursive descent pointers entirely. Root-level monotypes can represent uniformly distributed volumes with near-zero overhead.
*   **Relative Jumping:** Instead of full multi-byte heap pointers, nodes reference their structural children using variable-size relative byte pointers (`RelativePos`), packing spatial jumps concisely.

### Structural Mechanics: Hierarchical Data Binding and Routed Event Infrastructure

It is critical to explicitly delineate that the framework intentionally bypasses conventional object-oriented abstractions to maintain its minimal memory footprint. Specifically, traditional hierarchical data binding and routed event infrastructures are fundamentally inapplicable within this architecture.

*   **Absence of Data Binding:** The hierarchical nodes are intrinsically read-only, statically encoded segments within a byte buffer. Real-time data binding mechanisms are absent; structural modifications require complete volume reconstruction.
*   **Absence of Routed Events:** The framework does not implement a routed event infrastructure for spatial mutations or access notifications. Traversal is executed via deterministic, synchronous bitwise operations and relative byte offset navigation without event-driven overhead.

### Theoretical Models & Operational Hypotheses

The codebase maintains a strict segregation between established operational mechanics and experimental features.

*   **Established Framework (`Octree.cs`):** Executes deterministic, memory-safe data retrieval via bitwise traversal.
*   **Operational Hypotheses (`OctreeDev.cs`):** Contains experimental traversal algorithms attempting hardware-accelerated memory access. Specifically, it employs SIMD prefetching via `System.Runtime.Intrinsics.X86.Sse.Prefetch0(...)` while skipping sibling entries during traversal. This functionality remains a theoretical optimization and is strictly isolated from the established framework.

## Implementation Example: Data Retrieval

The `Get(int x, int y, int z)` function navigates the `Memory<byte>` segment via continuous bitwise adjustments and relative byte traversals. The following example utilizes contemporary .NET syntax to build and query the structural volume.

```csharp
using System;
using Tedd.Octree;

public static class Program
{
    public static void Main()
    {
        int levels = 4;
        int chunkSize = 1 << levels;

        // The Build function requires uncompressed voxel data mapped linearly.
        // Index mapping function: index = (x * chunkSize * chunkSize) + (y * chunkSize) + z
        uint[] voxels = new uint[chunkSize * chunkSize * chunkSize];

        // Structural instantiation
        Octree tree = new Octree(levels);

        // The span-based API accepts the voxel array via implicit conversion
        tree.Build(voxels);

        int queryX = 1;
        int queryY = 2;
        int queryZ = 3;

        try
        {
            // Extract value via spatial coordinates mapped to internal hierarchical bits
            uint nodeValue = tree.Get(queryX, queryY, queryZ);
            Console.WriteLine($"Extracted Node Data: {nodeValue}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Structural Retrieval Error: {ex.Message}");
        }
    }
}
```
