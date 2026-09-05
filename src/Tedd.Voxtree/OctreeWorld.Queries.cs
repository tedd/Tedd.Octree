using System;
using System.Runtime.InteropServices;

namespace Tedd.Voxtree;

public sealed partial class OctreeWorld
{
    private void ValidateBox(VoxelBox box)
    {
        if (box.MinX < 0 || box.MinY < 0 || box.MinZ < 0 || box.MaxX > SideLength || box.MaxY > SideLength || box.MaxZ > SideLength)
            throw new ArgumentOutOfRangeException(nameof(box));
    }
    /// <summary>Reads a channel voxel; false means outside the world or unloaded. Known-empty regions return zero.</summary>
    public bool TryGet(int channel, int x, int y, int z, out uint value)
    {
        ValidateChannel(channel); value = 0;
        if ((uint)x >= (uint)SideLength || (uint)y >= (uint)SideLength || (uint)z >= (uint)SideLength) return false;
        var link = Find(x, y, z, out _);
        if (link == 0) return false;
        if (link == -1) return true;
        var mask = ChunkSideLength - 1;
        value = _chunks.Span[-link - 2]!.GetChannel(channel).Get(x & mask, y & mask, z & mask);
        return true;
    }
    /// <summary>Reads a channel voxel, throwing for out-of-bounds or unloaded data.</summary>
    public uint Get(int channel, int x, int y, int z)
    {
        ValidatePoint(x, y, z);
        if (TryGet(channel, x, y, z, out var value)) return value;
        throw new InvalidOperationException("The queried voxel is unloaded.");
    }
    /// <summary>Determines occupancy. False means unloaded data prevents a conclusive answer.</summary>
    /// <remarks>A known match is conclusive even if other cells are unloaded. A false match result requires all intersecting data to be known.</remarks>
    public bool TryAny(VoxelBox box, int channel, VoxelFilter filter, out bool any)
    {
        ValidateBox(box); ValidateChannel(channel);
        var state = new WorldQuery { Channel = channel, Filter = filter };
        Visit(_root, Levels, 0, 0, 0, box, ref state);
        any = state.Found; return state.Found || !state.Unknown;
    }
    /// <summary>Tests occupancy, throwing only when unloaded data prevents a conclusive result.</summary>
    public bool Any(VoxelBox box, int channel, VoxelFilter filter)
    {
        if (TryAny(box, channel, filter, out var any)) return any;
        throw new InvalidOperationException("Unloaded data prevents an occupancy decision.");
    }
    /// <summary>True only if the complete queried channel region is known zero. Unloaded is not treated as air.</summary>
    public bool IsKnownEmpty(VoxelBox box, int channel = 0) => TryAny(box, channel, VoxelFilter.NonZero, out var any) && !any;
    /// <summary>Counts matching voxels in Int64. False means the returned count excludes unknown cells.</summary>
    public bool TryCountMatches(VoxelBox box, int channel, VoxelFilter filter, out long count)
    {
        ValidateBox(box); ValidateChannel(channel);
        var state = new WorldQuery { Channel = channel, Filter = filter, Counting = true };
        Visit(_root, Levels, 0, 0, 0, box, ref state);
        count = state.Count; return !state.Unknown;
    }
    private struct WorldQuery
    {
        internal int Channel;
        internal VoxelFilter Filter;
        internal bool Counting, Unknown, Found;
        internal long Count;
    }
    private static VoxelBox NodeBox(int x, int y, int z, int level)
    {
        var side = 1 << level; return new VoxelBox(x, y, z, x + side, y + side, z + side);
    }
    private void Visit(int link, int level, int x, int y, int z, VoxelBox box, ref WorldQuery state)
    {
        var clipped = box.Intersect(NodeBox(x, y, z, level));
        if (clipped.IsEmpty) return;
        if (link > 0)
        {
            var half = 1 << (level - 1);
            for (var i = 0; i < 8 && (state.Counting || !state.Found); i++)
                Visit(_nodes.Span[link - 1 + i], level - 1, x + (i >> 2) * half,
                    y + ((i >> 1) & 1) * half, z + (i & 1) * half, clipped, ref state);
            return;
        }
        if (link == 0) { state.Unknown = true; return; }
        if (link == -1)
        {
            if (state.Filter.Matches(0))
            {
                state.Found = true;
                state.Count += (long)(clipped.MaxX - clipped.MinX) * (clipped.MaxY - clipped.MinY) * (clipped.MaxZ - clipped.MinZ);
            }
            return;
        }
        var channel = _chunks.Span[-link - 2]!.GetChannel(state.Channel);
        var local = Translate(clipped, x, y, z);
        if (state.Counting) state.Count += channel.CountMatches(local, state.Filter);
        else state.Found = channel.Any(local, state.Filter);
    }
    private static VoxelBox Translate(VoxelBox box, int x, int y, int z) =>
        new(box.MinX - x, box.MinY - y, box.MinZ - z, box.MaxX - x, box.MaxY - y, box.MaxZ - z);

    /// <summary>Copies stored regions intersecting a box into caller storage; false indicates truncation.</summary>
    /// <remarks>Returned regions are not clipped and may be larger than the query. Uniform outer levels are never expanded into individual chunks.</remarks>
    public bool QueryRegions(VoxelBox box, Span<OctreeWorldRegion> destination, out int written)
    {
        ValidateBox(box); written = 0;
        return Collect(_root, Levels, 0, 0, 0, box, destination, ref written);
    }
    private bool Collect(int link, int level, int x, int y, int z, VoxelBox box, Span<OctreeWorldRegion> destination, ref int written)
    {
        var clipped = box.Intersect(NodeBox(x, y, z, level));
        if (clipped.IsEmpty) return true;
        if (link <= 0)
        {
            if (written == destination.Length) return false;
            destination[written++] = Region(link, x, y, z, level); return true;
        }
        var half = 1 << (level - 1);
        for (var i = 0; i < 8; i++)
            if (!Collect(_nodes.Span[link - 1 + i], level - 1, x + (i >> 2) * half,
                y + ((i >> 1) & 1) * half, z + (i & 1) * half, clipped, destination, ref written)) return false;
        return true;
    }
    /// <summary>Extracts all channels of a dense cubic block, crossing chunk boundaries as necessary.</summary>
    /// <remarks>Levels is 0..9; destination is channel-major. Any unloaded cell or overlap is rejected before writes.</remarks>
    public void CopyBlockTo(int x, int y, int z, int levels, Span<uint> destination, DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        var box = DenseBox(x, y, z, levels, layout);
        var count = OctreeCodec.GetVoxelCount(levels);
        PrepareCopy(box, destination, 0, ChannelCount, checked(count * ChannelCount));
        for (var channel = 0; channel < ChannelCount; channel++)
            CopyChannel(box, channel, destination.Slice(channel * count, count), layout);
    }
    /// <summary>Extracts one selected channel in linear or Morton order, crossing chunk boundaries.</summary>
    public void CopyChannelBlockTo(int channel, int x, int y, int z, int levels, Span<uint> destination, DenseVoxelLayout layout = DenseVoxelLayout.Linear)
    {
        ValidateChannel(channel); var box = DenseBox(x, y, z, levels, layout);
        var count = OctreeCodec.GetVoxelCount(levels);
        PrepareCopy(box, destination, channel, 1, count);
        CopyChannel(box, channel, destination[..count], layout);
    }
    /// <summary>Extracts an arbitrary box in one channel into packed linear X/Y/Z-major storage.</summary>
    public void CopyRegionTo(int channel, VoxelBox box, Span<uint> destination)
    {
        ValidateChannel(channel); ValidateBox(box); var count = box.Count;
        PrepareCopy(box, destination, channel, 1, count);
        CopyChannel(box, channel, destination[..count], DenseVoxelLayout.Linear);
    }
    private VoxelBox DenseBox(int x, int y, int z, int levels, DenseVoxelLayout layout)
    {
        OctreeCodec.ValidateLevels(levels); DenseVoxel.Validate(layout); var side = 1 << levels;
        var box = new VoxelBox(x, y, z, checked(x + side), checked(y + side), checked(z + side));
        ValidateBox(box); return box;
    }
    private void PrepareCopy(VoxelBox box, Span<uint> destination, int channel, int channels, int count)
    {
        if (destination.Length < count) throw new ArgumentException("Destination is too short.", nameof(destination));
        var bytes = MemoryMarshal.AsBytes(destination[..count]);
        if (bytes.Overlaps(MemoryMarshal.AsBytes(_nodes.Span)) || bytes.Overlaps(MemoryMarshal.AsBytes(_slotNext.Span)))
            throw new ArgumentException("Destination overlaps world workspace.");
        CheckReadable(_root, Levels, 0, 0, 0, box, bytes, channel, channels);
    }
    private void CheckReadable(int link, int level, int x, int y, int z, VoxelBox box, ReadOnlySpan<byte> destination, int channel, int channels)
    {
        var clipped = box.Intersect(NodeBox(x, y, z, level));
        if (clipped.IsEmpty || link == -1) return;
        if (link == 0) throw new InvalidOperationException("Cannot extract unloaded voxel data.");
        if (link < -1)
        {
            var chunk = _chunks.Span[-link - 2]!;
            for (var i = channel; i < channel + channels; i++)
                if (destination.Overlaps(chunk.GetChannelData(i).Span)) throw new ArgumentException("Destination overlaps a channel encoding.");
            return;
        }
        var half = 1 << (level - 1);
        for (var i = 0; i < 8; i++)
            CheckReadable(_nodes.Span[link - 1 + i], level - 1, x + (i >> 2) * half,
                y + ((i >> 1) & 1) * half, z + (i & 1) * half, clipped, destination, channel, channels);
    }
    private void CopyChannel(VoxelBox box, int channel, Span<uint> destination, DenseVoxelLayout layout)
    {
        destination.Clear(); // Known empty outer regions require no traversal or retained chunk bytes.
        CopyLeaves(_root, Levels, 0, 0, 0, box, channel, destination, layout);
    }
    private void CopyLeaves(int link, int level, int x, int y, int z, VoxelBox box, int channel, Span<uint> destination, DenseVoxelLayout layout)
    {
        if (link >= -1 && link <= 0) return;
        var clipped = box.Intersect(NodeBox(x, y, z, level));
        if (clipped.IsEmpty) return;
        if (link < -1)
        {
            var state = new OctreeQueries.State(OctreeQueries.QueryKind.Copy, Translate(clipped, x, y, z), default)
            { Values = destination, CopyBounds = Translate(box, x, y, z), OutputLayout = layout };
            OctreeQueries.Run(_chunks.Span[-link - 2]!.GetChannel(channel), ref state);
            return;
        }
        var half = 1 << (level - 1);
        for (var i = 0; i < 8; i++)
            CopyLeaves(_nodes.Span[link - 1 + i], level - 1, x + (i >> 2) * half,
                y + ((i >> 1) & 1) * half, z + (i & 1) * half, box, channel, destination, layout);
    }
}
