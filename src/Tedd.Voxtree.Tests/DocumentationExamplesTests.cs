using Tedd.Voxtree;

namespace Tedd.Voxtree.Tests;

// Executable counterpart of docs/BULK_STREAMING.md. Keep examples synchronized.
public class DocumentationExamplesTests
{
    [Fact]
    public void BulkAndStreamingGuideExamplesExecute()
    {
        var packetPath = Path.Combine(Path.GetTempPath(), "octree-guide-" + Guid.NewGuid().ToString("N") + ".octree");
        try
        {

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

            var tree = new Octree(levels);
            tree.Build(morton, DenseVoxelLayout.Morton);

            // Allocate once and reuse; Build itself performs no managed allocation.
            var encoded = new byte[Octree.GetMaximumSize(levels)];
            int written = Octree.Build(morton, levels, encoded, DenseVoxelLayout.Morton);
            var view = new OctreeSpan(encoded.AsSpan(0, written));

            // Exact sizing is available when retaining minimal encoded capacity matters.
            int exact = Octree.GetRequiredSize(morton, levels, DenseVoxelLayout.Morton);
            bool built = Octree.TryBuild(morton, levels, encoded, out written, DenseVoxelLayout.Morton);

            var smallLinear = new uint[8 * 8 * 8];
            var smallMorton = new uint[smallLinear.Length];
            tree.CopyBlockTo(3, 2, 1, levels: 3, smallLinear);
            view.CopyBlockTo(3, 2, 1, levels: 3, smallMorton, DenseVoxelLayout.Morton);

            // Arbitrary rectangular extraction remains available in packed linear order.
            var box = new VoxelBox(3, 2, 1, 11, 6, 9);
            var rectangular = new uint[box.Count];
            tree.CopyRegionTo(box, rectangular);

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

            // Retain Morton storage across repeated edits, then encode every channel once.
            HotOctreeChunk hot = chunk.MarkHot();
            hot[0, 1, 2, 3] = 43;
            hot.GetChannelSpan(3).Clear();
            OctreeChunk hotSnapshot = hot.UnmarkHot();

            // These memories must remain alive and immutable while the chunk is retained.
            var encodings = new ReadOnlyMemory<byte>[channels];
            for (int c = 0; c < channels; c++)
            {
                var bytes = new byte[Octree.GetMaximumSize(levels)]; // Provision or rent storage here.
                int used = Octree.Build(dense.GetChannelSpan(c), levels, bytes, DenseVoxelLayout.Morton);
                encodings[c] = bytes.AsMemory(0, used);
            }
            var borrowed = new OctreeChunk(levels, encodings);

            var globalWorld = new WorldEntity(chunkSize: 32, channelCount: channels);
            globalWorld.SetChunk(chunkX: -1, chunkY: 0, chunkZ: 2, chunk);
            uint globalMaterial = globalWorld.Get(channel: 0, x: -31, y: 2, z: 67);
            var globalHot = globalWorld.MarkChunkHot(-1, 0, 2);
            globalHot[0, 1, 2, 3] = 44;
            globalWorld.CommitHotChunk(-1, 0, 2, globalHot);

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

            bool complete = world.TryCountMatches(area, 0, VoxelFilter.NonZero, out long solidCount);
            OctreeWorldRegion region = world.GetRegion(100, 100, 100);
            // region.Kind: Unloaded, Empty, or Loaded; region.Levels describes the stored extent.

            var allAir = new OctreeWorld(20, 5, 4, chunkCapacity: 0, initiallyEmpty: true);
            bool empty = allAir.IsKnownEmpty(new VoxelBox(0, 0, 0, 1 << 20, 1 << 20, 1 << 20));
            // One known-empty root, no retained chunks, no live branches.

            // All intersected cells must be known before any output is written.
            var window = new uint[count * channels];
            world.CopyBlockTo(0, 0, 0, levels: 5, window, DenseVoxelLayout.Morton);
            var oneChannel = new uint[count];
            world.CopyChannelBlockTo(0, 0, 0, 0, levels: 5, oneChannel);
            var narrow = new VoxelBox(0, 0, 0, 16, 8, 16);
            var narrowValues = new uint[narrow.Count];
            world.CopyRegionTo(channel: 0, narrow, narrowValues);

            if (world.TryGetChunk(0, 0, 0, out OctreeChunk? resident))
            {
                byte[] packet = new byte[resident!.SerializedLength];
                int used = resident.CopyEncodedTo(packet);
                File.WriteAllBytes(packetPath, packet); // I/O/allocation is outside the hot path.

                bool evicted = world.TryUnloadRegion(0, 0, 0, levels: 5);
                // Eviction makes this region unknown, not known air.

                byte[] loadedPacket = File.ReadAllBytes(packetPath);
                OctreeChunk loaded = OctreeChunk.FromEncoded(loadedPacket.AsMemory(0, used));
                world.LoadChunk(0, 0, 0, loaded);
            }

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

            Assert.True(empty);
            Assert.True(built);
            Assert.Equal(42u, value);
            Assert.Equal(42u, globalMaterial);
            Assert.Equal(43u, hotSnapshot.GetChannel(0).Get(1, 2, 3));
            Assert.Equal(44u, globalWorld.Get(0, -31, 2, 67));
        }
        finally { File.Delete(packetPath); }
    }
}
