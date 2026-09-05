using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Tedd.Voxtree.Benchmark.Archive.MortonBefore;

internal static class OctreeQueries
{
    internal enum QueryKind { Any, Count, Collect, Copy, Nearest }

    internal static void Validate(OctreeSpan tree, VoxelBox box)
    {
        if (!tree.IsValid)
            throw new InvalidOperationException("The octree view is not initialized.");
        var side = tree.SideLength;
        if (box.MinX < 0 || box.MinY < 0 || box.MinZ < 0 ||
            box.MaxX > side || box.MaxY > side || box.MaxZ > side)
            throw new ArgumentOutOfRangeException(nameof(box), "Query bounds must lie within the volume.");
    }

    internal static VoxelBox RadiusBox(OctreeSpan tree, int x, int y, int z, int radius)
    {
        if (!tree.IsValid)
            throw new InvalidOperationException("The octree view is not initialized.");
        if (!tree.Contains(x, y, z))
            throw new ArgumentOutOfRangeException(nameof(x), "The query center must lie within the volume.");
        if (radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var side = tree.SideLength;
        var r = Math.Min(radius, side);
        return new VoxelBox(Math.Max(0, x - r), Math.Max(0, y - r), Math.Max(0, z - r),
            Math.Min(side, x + r + 1), Math.Min(side, y + r + 1), Math.Min(side, z + r + 1));
    }

    internal static void Run(OctreeSpan tree, ref State state)
    {
        if (state.Box.IsEmpty)
            return;
        var data = tree.Data;
        var kind = OctreeCodec.GetStorageKindUnchecked(data);
        if (kind == StorageKind.Dense)
        {
            Dense(data, tree.SideLength, ref state);
            return;
        }
        if (kind == StorageKind.Uniform)
        {
            var cursor = OctreeCodec.HeaderSize;
            if (!OctreeCodec.TryReadVarUInt(data, ref cursor, out var value))
                throw new FormatException("Malformed uniform value.");
            if (state.Kind == QueryKind.Copy && state.RingSide == 0 &&
                state.Box.MinX == state.CopyBounds.MinX && state.Box.MinY == state.CopyBounds.MinY &&
                state.Box.MinZ == state.CopyBounds.MinZ && state.Box.MaxX == state.CopyBounds.MaxX &&
                state.Box.MaxY == state.CopyBounds.MaxY && state.Box.MaxZ == state.CopyBounds.MaxZ)
            {
                state.Values[..state.Box.Count].Fill(value); // A constant block has the same dense representation in either order.
                return;
            }
            state.Accept(state.Box, value);
            return;
        }
        var body = data[..^1];
        Visit(body, body.Length - data[^1], tree.Levels, 0, 0, 0, ref state);
    }

    private static void Visit(ReadOnlySpan<byte> data, int offset, int level,
        int x, int y, int z, ref State state)
    {
        if (level <= 0 || offset < OctreeCodec.HeaderSize || offset >= data.Length)
            throw new FormatException("Malformed tree node.");
        var cursor = offset;
        var mask = data[cursor++];
        Span<uint> tokens = stackalloc uint[8];
        for (var child = 0; child < 8; child++)
            if (!OctreeCodec.TryReadVarUInt(data, ref cursor, out tokens[child]))
                throw new FormatException("Malformed tree token.");

        var half = 1 << (level - 1);
        // Visit the octant containing the center first to tighten nearest bounds early.
        var preferred = state.Kind == QueryKind.Nearest
            ? ((state.X >= x + half ? 1 : 0) << 2) |
              ((state.Y >= y + half ? 1 : 0) << 1) | (state.Z >= z + half ? 1 : 0)
            : 0;
        for (var order = 0; order < 8 && !state.Done; order++)
        {
            var child = order ^ preferred;
            var cx = x + ((child >> 2) * half);
            var cy = y + (((child >> 1) & 1) * half);
            var cz = z + ((child & 1) * half);
            var clipped = state.Box.Intersect(new VoxelBox(cx, cy, cz, cx + half, cy + half, cz + half));
            if (clipped.IsEmpty ||
                (state.Kind == QueryKind.Nearest && clipped.DistanceSquared(state.X, state.Y, state.Z) > state.BestDistance))
                continue;
            if ((mask & (1 << child)) != 0)
                state.Accept(clipped, tokens[child]);
            else
            {
                var distance = tokens[child];
                if (level == 1 || distance == 0 || distance > (uint)(offset - OctreeCodec.HeaderSize))
                    throw new FormatException("Malformed child offset.");
                Visit(data, offset - (int)distance, level - 1, cx, cy, cz, ref state);
            }
        }
    }

    private static void Dense(ReadOnlySpan<byte> data, int side, ref State state)
    {
        var box = state.Box;
        for (var x = box.MinX; x < box.MaxX && !state.Done; x++)
        for (var y = box.MinY; y < box.MaxY && !state.Done; y++)
        {
            var start = OctreeCodec.HeaderSize + (((x * side + y) * side + box.MinZ) * sizeof(uint));
            var rowBytes = data.Slice(start, (box.MaxZ - box.MinZ) * sizeof(uint));
            if (BitConverter.IsLittleEndian)
            {
                var row = MemoryMarshal.Cast<byte, uint>(rowBytes);
                if (state.Kind == QueryKind.Count)
                {
                    state.Count += state.Filter.CountIn(row);
                    continue;
                }
                if (state.Kind == QueryKind.Any)
                {
                    if (state.Filter.AnyIn(row))
                    {
                        state.Count = 1;
                        state.Done = true;
                    }
                    continue;
                }
                if (state.Kind == QueryKind.Copy)
                {
                    state.CopyRow(x, y, box.MinZ, row);
                    continue;
                }
            }
            for (var z = box.MinZ; z < box.MaxZ && !state.Done; z++)
            {
                var value = BinaryPrimitives.ReadUInt32LittleEndian(rowBytes.Slice((z - box.MinZ) * sizeof(uint), sizeof(uint)));
                state.Accept(new VoxelBox(x, y, z, x + 1, y + 1, z + 1), value);
            }
        }
    }

    internal ref struct State
    {
        internal QueryKind Kind;
        internal VoxelBox Box;
        internal VoxelBox CopyBounds;
        internal VoxelFilter Filter;
        internal Span<VoxelHit> Hits;
        internal Span<uint> Values;
        internal int RingSide;
        internal DenseVoxelLayout OutputLayout;
        internal int Count;
        internal bool Done;
        internal bool Truncated;
        internal int X, Y, Z;
        internal long BestDistance;
        internal VoxelHit Nearest;
        internal bool Found;

        internal State(QueryKind kind, VoxelBox box, VoxelFilter filter)
        {
            this = default;
            Kind = kind; Box = box; CopyBounds = box; Filter = filter;
        }

        internal void Accept(VoxelBox box, uint value)
        {
            if (Kind == QueryKind.Copy)
            {
                if (OutputLayout == DenseVoxelLayout.Morton)
                {
                    var side = box.MaxX - box.MinX;
                    var mask = side - 1;
                    if ((side & mask) == 0 && side == box.MaxY - box.MinY && side == box.MaxZ - box.MinZ &&
                        (((box.MinX - CopyBounds.MinX) | (box.MinY - CopyBounds.MinY) | (box.MinZ - CopyBounds.MinZ)) & mask) == 0)
                    {
                        // An aligned octant is one contiguous Morton interval, including within a larger output block.
                        Values.Slice(RowOffset(box.MinX, box.MinY, box.MinZ), box.Count).Fill(value);
                        return;
                    }
                }
                for (var x = box.MinX; x < box.MaxX; x++)
                for (var y = box.MinY; y < box.MaxY; y++)
                    FillRow(x, y, box.MinZ, box.MaxZ - box.MinZ, value);
                return;
            }
            if (!Filter.Matches(value))
                return;
            if (Kind == QueryKind.Any)
            {
                Count = 1; Done = true;
            }
            else if (Kind == QueryKind.Count)
                Count += box.Count;
            else if (Kind == QueryKind.Nearest)
            {
                var x = VoxelBox.Clamp(X, box.MinX, box.MaxX - 1);
                var y = VoxelBox.Clamp(Y, box.MinY, box.MaxY - 1);
                var z = VoxelBox.Clamp(Z, box.MinZ, box.MaxZ - 1);
                var dx = X - x; var dy = Y - y; var dz = Z - z;
                var distance = (long)dx * dx + (long)dy * dy + (long)dz * dz;
                if (distance > BestDistance ||
                    (Found && distance == BestDistance &&
                     (x > Nearest.X || (x == Nearest.X && (y > Nearest.Y || (y == Nearest.Y && z >= Nearest.Z))))))
                    return;
                BestDistance = distance; Nearest = new VoxelHit(x, y, z, value); Found = true;
                if (distance == 0) Done = true;
            }
            else
            {
                for (var x = box.MinX; x < box.MaxX; x++)
                for (var y = box.MinY; y < box.MaxY; y++)
                for (var z = box.MinZ; z < box.MaxZ; z++)
                {
                    if (Count == Hits.Length)
                    {
                        Truncated = true; Done = true; return;
                    }
                    Hits[Count++] = new VoxelHit(x, y, z, value);
                }
            }
        }

        private int RowOffset(int x, int y, int z)
        {
            if (OutputLayout == DenseVoxelLayout.Morton)
                return DenseVoxel.Index(x - CopyBounds.MinX, y - CopyBounds.MinY, z - CopyBounds.MinZ,
                    CopyBounds.MaxX - CopyBounds.MinX, OutputLayout);
            if (RingSide != 0)
            {
                var mask = RingSide - 1;
                return (((x & mask) * RingSide + (y & mask)) * RingSide) + (z & mask);
            }
            return (((x - CopyBounds.MinX) * (CopyBounds.MaxY - CopyBounds.MinY) +
                y - CopyBounds.MinY) * (CopyBounds.MaxZ - CopyBounds.MinZ)) + z - CopyBounds.MinZ;
        }

        private void FillRow(int x, int y, int z, int count, uint value)
        {
            if (OutputLayout == DenseVoxelLayout.Morton)
            {
                for (var i = 0; i < count; i++) Values[RowOffset(x, y, z + i)] = value;
                return;
            }
            var first = RingSide == 0 ? count : Math.Min(count, RingSide - (z & (RingSide - 1)));
            Values.Slice(RowOffset(x, y, z), first).Fill(value);
            if (first != count)
                Values.Slice(RowOffset(x, y, z + first), count - first).Fill(value);
        }

        internal void CopyRow(int x, int y, int z, ReadOnlySpan<uint> row)
        {
            if (OutputLayout == DenseVoxelLayout.Morton)
            {
                for (var i = 0; i < row.Length; i++) Values[RowOffset(x, y, z + i)] = row[i];
                return;
            }
            var first = RingSide == 0 ? row.Length : Math.Min(row.Length, RingSide - (z & (RingSide - 1)));
            row[..first].CopyTo(Values.Slice(RowOffset(x, y, z), first));
            if (first != row.Length)
                row[first..].CopyTo(Values.Slice(RowOffset(x, y, z + first), row.Length - first));
        }
    }
}
