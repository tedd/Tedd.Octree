## 2024-07-05 - Octree Structural Exception Path Coverage
**Observation:** The `Get(int x, int y, int z)` function within `Octree.cs` and `OctreeDev.cs` throws an Exception if invalid structure traversal completes without discovering a leaf node. Furthermore, testing coverage lacked validation for root nodes behaving precisely as a Monotype node without branching logic.

**Strategic Action:**
- Addressed code logic anomaly causing index out of bounds regarding memory structure in `OctreeDev.cs` when testing missing exception scenarios where `length > 64` conditions failed due to out of bounds arrays using `.MoveSize()` inside structural leaps (`MoveReadSize`). The `span.MoveSize()` operation assumes data structures over a given length instead of relying on exact index locations in test memory constructs.
- Added `ExceptionTest.cs` to explicitly trigger structural data failures and assert that `Exception("Error reading octree data structure.")` is appropriately thrown for both `Octree` and `OctreeDev`.
- Added `MonotypeRootTest.cs` leveraging reflection to mock simple root monotype byte arrays. This guarantees edge cases directly parsing header and reading scalar size from the immediate node executes safely, covering monotype branch termination correctly.
