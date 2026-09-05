using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tedd.Octree;

internal static class OctreeCodec
{
    internal const byte Magic = 0x4f; // 'O'
    internal const int HeaderSize = 2;
    internal const int MaximumNodeSize = 1 + (8 * 5);
    internal const int MinimumNodeSize = 1 + 8;

    private const int VersionShift = 6;
    private const byte VersionMask = 0xc0;
    private const int LevelShift = 2;
    private const byte LevelMask = 0x3c;
    private const byte StorageKindMask = 0x03;

    internal static void ValidateLevels(int levels)
    {
        if ((uint)levels > Octree.MaxLevels)
            throw new ArgumentOutOfRangeException(
                nameof(levels),
                levels,
                $"Levels must be in the inclusive range 0 through {Octree.MaxLevels}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetVoxelCount(int levels) => 1 << (levels * 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StorageKind GetStorageKindUnchecked(ReadOnlySpan<byte> data) =>
        (StorageKind)(data[1] & StorageKindMask);

    internal static int GetRequiredSize(ReadOnlySpan<uint> source, int levels) =>
        CreateBuildPlan(source, levels).EncodedLength;

    internal static byte[] BuildOwned(ReadOnlySpan<uint> source, int levels)
    {
        var plan = CreateBuildPlan(source, levels);
#if NET10_0_OR_GREATER
        // Every byte is written before publication; avoid clearing a large dense payload twice.
        var encoded = GC.AllocateUninitializedArray<byte>(plan.EncodedLength);
#else
        var encoded = new byte[plan.EncodedLength];
#endif

        if (plan.StorageKind == StorageKind.Uniform)
        {
            var writer = new OctreeWriter(encoded, measureOnly: false);
            if (!writer.WriteVarUInt(plan.UniformValue) || writer.Position != encoded.Length)
                throw new InvalidOperationException("The octree encoder produced an inconsistent uniform result.");

            WriteHeader(encoded, levels, StorageKind.Uniform);
            return encoded;
        }

        if (plan.StorageKind == StorageKind.Dense)
        {
            WriteDense(source, encoded.AsSpan(HeaderSize));
            WriteHeader(encoded, levels, StorageKind.Dense);
            return encoded;
        }

        if (!TryBuild(source, levels, encoded, out var written) || written != encoded.Length)
            throw new InvalidOperationException("The octree encoder produced an inconsistent tree result.");

        return encoded;
    }

    private static BuildPlan CreateBuildPlan(ReadOnlySpan<uint> source, int levels)
    {
        ValidateSource(source, levels);
        var denseLength = GetDenseSize(source.Length);

        var writer = new OctreeWriter(Span<byte>.Empty, measureOnly: true, denseLength);
        if (!BuildSubtree(
                source,
                levels,
                baseIndex: 0,
                rowStride: 1 << levels,
                planeStride: 1 << (levels * 2),
                ref writer,
                out var root))
        {
            return BuildPlan.Dense(denseLength);
        }

        if (root.IsUniform)
        {
            if (!writer.WriteVarUInt(root.Value))
                return BuildPlan.Dense(denseLength);

            return writer.Position < denseLength
                ? BuildPlan.Uniform(writer.Position, root.Value)
                : BuildPlan.Dense(denseLength);
        }

        var rootLength = writer.Position - root.NodeOffset;
        if ((uint)(rootLength - MinimumNodeSize) > MaximumNodeSize - MinimumNodeSize)
            throw new InvalidOperationException("The octree encoder produced an invalid root node.");
        if (!writer.WriteByte((byte)rootLength))
            return BuildPlan.Dense(denseLength);

        return writer.Position < denseLength
            ? BuildPlan.Tree(writer.Position)
            : BuildPlan.Dense(denseLength);
    }

    internal static int GetMaximumSize(int levels)
    {
        ValidateLevels(levels);
        return GetDenseSize(GetVoxelCount(levels));
    }

    internal static bool TryBuild(
        ReadOnlySpan<uint> source,
        int levels,
        Span<byte> destination,
        out int bytesWritten)
    {
        ValidateSource(source, levels);

        if (MemoryMarshal.AsBytes(source).Overlaps(destination))
            throw new ArgumentException("Source and destination must not overlap.", nameof(destination));

        bytesWritten = 0;
        if (destination.Length < HeaderSize + 1)
            return false;

        var denseLength = GetDenseSize(source.Length);
        var speculativeLength = Math.Min(destination.Length, denseLength);
        var writer = new OctreeWriter(destination[..speculativeLength], measureOnly: false);
        var treeCompleted = BuildSubtree(
                source,
                levels,
                baseIndex: 0,
                rowStride: 1 << levels,
                planeStride: 1 << (levels * 2),
                ref writer,
                out var root);

        if (treeCompleted && root.IsUniform)
        {
            if (!writer.WriteVarUInt(root.Value))
                treeCompleted = false;
        }
        else if (treeCompleted)
        {
            var rootLength = writer.Position - root.NodeOffset;
            if ((uint)(rootLength - MinimumNodeSize) > MaximumNodeSize - MinimumNodeSize)
                treeCompleted = false;
            else if (!writer.WriteByte((byte)rootLength))
                treeCompleted = false;
        }

        if (!treeCompleted || writer.Position >= denseLength)
        {
            if (destination.Length < denseLength)
                return false;

            WriteDense(source, destination[HeaderSize..denseLength]);
            WriteHeader(destination, levels, StorageKind.Dense);
            bytesWritten = denseLength;
            return true;
        }

        WriteHeader(destination, levels, root.IsUniform ? StorageKind.Uniform : StorageKind.Tree);
        bytesWritten = writer.Position;
        return true;
    }

    internal static bool TryReadHeader(
        ReadOnlySpan<byte> data,
        out int levels,
        out StorageKind storageKind)
    {
        levels = default;
        storageKind = default;

        if (data.Length < HeaderSize + 1 || data[0] != Magic)
            return false;

        var metadata = data[1];
        var version = (metadata & VersionMask) >> VersionShift;
        if (version != Octree.FormatVersion)
            return false;

        levels = (metadata & LevelMask) >> LevelShift;
        if ((uint)levels > Octree.MaxLevels)
            return false;

        storageKind = (StorageKind)(metadata & StorageKindMask);
        if (storageKind > StorageKind.Dense)
            return false;

        if (storageKind == StorageKind.Uniform)
        {
            var cursor = HeaderSize;
            return TryReadVarUInt(data, ref cursor, out _) && cursor == data.Length;
        }

        if (storageKind == StorageKind.Dense)
            return data.Length == GetDenseSize(GetVoxelCount(levels));

        if (levels == 0 || data.Length < HeaderSize + MinimumNodeSize + 1)
            return false;

        var rootLength = data[^1];
        return rootLength is >= MinimumNodeSize and <= MaximumNodeSize &&
               data.Length - 1 - rootLength >= HeaderSize;
    }

    internal static bool IsWellFormed(
        ReadOnlySpan<byte> data,
        int levels,
        StorageKind storageKind)
    {
        if (!TryReadHeader(data, out var encodedLevels, out var encodedKind) ||
            encodedLevels != levels ||
            encodedKind != storageKind)
        {
            return false;
        }

        if (storageKind is StorageKind.Uniform or StorageKind.Dense)
            return true;

        var bodyEnd = data.Length - 1;
        var rootLength = data[^1];
        var rootOffset = bodyEnd - rootLength;

        return TryValidateNode(
                   data[..bodyEnd],
                   rootOffset,
                   levels,
                   out var subtreeStart,
                   out var nodeEnd) &&
               subtreeStart == HeaderSize &&
               nodeEnd == bodyEnd &&
               nodeEnd - rootOffset == rootLength;
    }

    internal static bool TryGet(
        ReadOnlySpan<byte> data,
        int levels,
        StorageKind storageKind,
        int x,
        int y,
        int z,
        out uint value)
    {
        value = default;
        var sideLength = 1 << levels;
        if ((uint)x >= (uint)sideLength ||
            (uint)y >= (uint)sideLength ||
            (uint)z >= (uint)sideLength)
        {
            return false;
        }

        return TryGetUnchecked(data, levels, storageKind, x, y, z, out value);
    }

    internal static bool TryGetUnchecked(
        ReadOnlySpan<byte> data,
        int levels,
        StorageKind storageKind,
        int x,
        int y,
        int z,
        out uint value)
    {
        value = default;
        var sideLength = 1 << levels;

        if (storageKind == StorageKind.Uniform)
        {
            var cursor = HeaderSize;
            return TryReadVarUInt(data, ref cursor, out value) && cursor == data.Length;
        }

        if (storageKind == StorageKind.Dense)
        {
            var flatIndex = (x * sideLength * sideLength) + (y * sideLength) + z;
            var byteOffset = HeaderSize + (flatIndex * sizeof(uint));
            value = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(byteOffset, sizeof(uint)));
            return true;
        }

        if (levels == 0 || data.Length < HeaderSize + MinimumNodeSize + 1)
            return false;

        var body = data[..^1];
        var rootLength = data[^1];
        var nodeOffset = body.Length - rootLength;
        if (rootLength is < MinimumNodeSize or > MaximumNodeSize ||
            nodeOffset < HeaderSize)
        {
            return false;
        }

        for (var bit = levels - 1; bit >= 0; bit--)
        {
            var nodeStart = nodeOffset;
            if ((uint)nodeStart >= (uint)body.Length)
                return false;

            var cursor = nodeStart;
            var uniformMask = body[cursor++];
            var child = (((x >> bit) & 1) << 2) |
                        (((y >> bit) & 1) << 1) |
                        ((z >> bit) & 1);

            for (var index = 0; index < child; index++)
            {
                if (!TryReadVarUInt(body, ref cursor, out _))
                    return false;
            }

            if (!TryReadVarUInt(body, ref cursor, out var token))
                return false;

            if ((uniformMask & (1 << child)) != 0)
            {
                value = token;
                return true;
            }

            if (bit == 0 || token == 0 || token > (uint)(nodeStart - HeaderSize))
                return false;

            nodeOffset = nodeStart - (int)token;
        }

        return false;
    }

    internal static bool TryCopyTo(
        ReadOnlySpan<byte> data,
        int levels,
        StorageKind storageKind,
        Span<uint> destination)
    {
        var count = GetVoxelCount(levels);
        if (destination.Length < count ||
            MemoryMarshal.AsBytes(destination[..count]).Overlaps(data) ||
            !IsWellFormed(data, levels, storageKind))
            return false;

        destination = destination[..count];
        if (storageKind == StorageKind.Uniform)
        {
            var cursor = HeaderSize;
            if (!TryReadVarUInt(data, ref cursor, out var value))
                return false;
            destination.Fill(value);
            return true;
        }

        if (storageKind == StorageKind.Dense)
        {
            ReadDense(data[HeaderSize..], destination);
            return true;
        }

        var body = data[..^1];
        var rootOffset = body.Length - data[^1];
        return TryDecodeNode(
            body,
            rootOffset,
            levels,
            sideLength: 1 << levels,
            x: 0,
            y: 0,
            z: 0,
            destination);
    }

    private static void ValidateSource(ReadOnlySpan<uint> source, int levels)
    {
        ValidateLevels(levels);
        var expected = GetVoxelCount(levels);
        if (source.Length != expected)
            throw new ArgumentException(
                $"Source must contain exactly {expected} elements for {levels} levels; it contains {source.Length}.",
                nameof(source));
    }

    private static int GetDenseSize(int valueCount) =>
        checked(HeaderSize + (valueCount * sizeof(uint)));

    private static void WriteHeader(Span<byte> destination, int levels, StorageKind storageKind)
    {
        destination[0] = Magic;
        destination[1] = (byte)(
            (Octree.FormatVersion << VersionShift) |
            (levels << LevelShift) |
            (byte)storageKind);
    }

    private static void WriteDense(ReadOnlySpan<uint> source, Span<byte> destination)
    {
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.AsBytes(source).CopyTo(destination);
            return;
        }

        for (var index = 0; index < source.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(index * sizeof(uint), sizeof(uint)), source[index]);
    }

    private static void ReadDense(ReadOnlySpan<byte> source, Span<uint> destination)
    {
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.Cast<byte, uint>(source).CopyTo(destination);
            return;
        }

        for (var index = 0; index < destination.Length; index++)
            destination[index] = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(index * sizeof(uint), sizeof(uint)));
    }

    private static bool BuildSubtree(
        ReadOnlySpan<uint> source,
        int level,
        int baseIndex,
        int rowStride,
        int planeStride,
        ref OctreeWriter writer,
        out SubtreeResult result)
    {
        if (level == 0)
        {
            result = SubtreeResult.Uniform(source[baseIndex]);
            return true;
        }

#if NET10_0_OR_GREATER
        // Runtime span searches select the supported SIMD width. Collapse a
        // uniform region before visiting its descendants.
        if (level >= 2 && IsUniformRegion(source, baseIndex, 1 << level, rowStride, planeStride))
        {
            result = SubtreeResult.Uniform(source[baseIndex]);
            return true;
        }
#endif

        var subtreeStart = writer.Position;
        Span<SubtreeResult> children = stackalloc SubtreeResult[8];
        var half = 1 << (level - 1);
        var allUniform = true;
        var firstValue = 0u;

        for (var child = 0; child < 8; child++)
        {
            var childBase = baseIndex +
                            (((child >> 2) & 1) * half * planeStride) +
                            (((child >> 1) & 1) * half * rowStride) +
                            ((child & 1) * half);

            if (!BuildSubtree(
                    source,
                    level - 1,
                    childBase,
                    rowStride,
                    planeStride,
                    ref writer,
                    out children[child]))
            {
                result = default;
                return false;
            }

            if (child == 0)
                firstValue = children[child].Value;

            if (!children[child].IsUniform || children[child].Value != firstValue)
                allUniform = false;
        }

        if (allUniform)
        {
            writer.Rewind(subtreeStart);
            result = SubtreeResult.Uniform(firstValue);
            return true;
        }

        byte uniformMask = 0;
        for (var child = 0; child < 8; child++)
        {
            if (children[child].IsUniform)
                uniformMask |= (byte)(1 << child);
        }

        var nodeOffset = writer.Position;
        if (!writer.WriteByte(uniformMask))
        {
            result = default;
            return false;
        }

        for (var child = 0; child < 8; child++)
        {
            var token = children[child].IsUniform
                ? children[child].Value
                : (uint)(nodeOffset - children[child].NodeOffset);

            if (!writer.WriteVarUInt(token))
            {
                result = default;
                return false;
            }
        }

        result = SubtreeResult.Node(nodeOffset);
        return true;
    }

#if NET10_0_OR_GREATER
    private static bool IsUniformRegion(ReadOnlySpan<uint> source, int start, int size, int rowStride, int planeStride)
    {
        var value = source[start];
        if (size == rowStride)
            return source.Slice(start, size * size * size).IndexOfAnyExcept(value) < 0;
        for (var x = 0; x < size; x++)
        {
            var plane = start + x * planeStride;
            for (var y = 0; y < size; y++)
                if (source.Slice(plane + y * rowStride, size).IndexOfAnyExcept(value) >= 0)
                    return false;
        }
        return true;
    }
#endif

    private static bool TryValidateNode(
        ReadOnlySpan<byte> body,
        int nodeOffset,
        int level,
        out int subtreeStart,
        out int nodeEnd)
    {
        subtreeStart = default;
        nodeEnd = default;

        if (level <= 0 || (uint)nodeOffset >= (uint)body.Length)
            return false;

        var cursor = nodeOffset;
        var uniformMask = body[cursor++];
        Span<uint> tokens = stackalloc uint[8];

        for (var child = 0; child < 8; child++)
        {
            if (!TryReadVarUInt(body, ref cursor, out tokens[child]))
                return false;
        }

        nodeEnd = cursor;
        if (level == 1 && uniformMask != byte.MaxValue)
            return false;

        if (uniformMask == byte.MaxValue)
        {
            var value = tokens[0];
            var collapsible = true;
            for (var child = 1; child < 8; child++)
                collapsible &= tokens[child] == value;
            if (collapsible)
                return false;
        }

        var expectedEnd = nodeOffset;
        for (var child = 7; child >= 0; child--)
        {
            if ((uniformMask & (1 << child)) != 0)
                continue;

            var distance = tokens[child];
            if (distance == 0 || distance > (uint)(nodeOffset - HeaderSize))
                return false;

            var childOffset = nodeOffset - (int)distance;
            if (!TryValidateNode(
                    body,
                    childOffset,
                    level - 1,
                    out var childStart,
                    out var childEnd) ||
                childEnd != expectedEnd)
            {
                return false;
            }

            expectedEnd = childStart;
        }

        subtreeStart = expectedEnd;
        return true;
    }

    private static bool TryDecodeNode(
        ReadOnlySpan<byte> body,
        int nodeOffset,
        int level,
        int sideLength,
        int x,
        int y,
        int z,
        Span<uint> destination)
    {
        var cursor = nodeOffset;
        if ((uint)cursor >= (uint)body.Length)
            return false;

        var uniformMask = body[cursor++];
        Span<uint> tokens = stackalloc uint[8];
        for (var child = 0; child < 8; child++)
        {
            if (!TryReadVarUInt(body, ref cursor, out tokens[child]))
                return false;
        }

        var half = 1 << (level - 1);
        for (var child = 0; child < 8; child++)
        {
            var childX = x + (((child >> 2) & 1) * half);
            var childY = y + (((child >> 1) & 1) * half);
            var childZ = z + ((child & 1) * half);

            if ((uniformMask & (1 << child)) != 0)
            {
                FillRegion(destination, sideLength, childX, childY, childZ, half, tokens[child]);
                continue;
            }

            if (level == 1 ||
                !TryDecodeNode(
                    body,
                    nodeOffset - (int)tokens[child],
                    level - 1,
                    sideLength,
                    childX,
                    childY,
                    childZ,
                    destination))
            {
                return false;
            }
        }

        return true;
    }

    private static void FillRegion(
        Span<uint> destination,
        int sideLength,
        int x,
        int y,
        int z,
        int size,
        uint value)
    {
        var planeStride = sideLength * sideLength;
        for (var offsetX = 0; offsetX < size; offsetX++)
        {
            var plane = (x + offsetX) * planeStride;
            for (var offsetY = 0; offsetY < size; offsetY++)
            {
                var start = plane + ((y + offsetY) * sideLength) + z;
                destination.Slice(start, size).Fill(value);
            }
        }
    }

    internal static bool TryReadVarUInt(
        ReadOnlySpan<byte> data,
        ref int offset,
        out uint value)
    {
        value = default;
        var start = offset;
        var shift = 0;

        while (shift <= 28)
        {
            if ((uint)offset >= (uint)data.Length)
                return false;

            var current = data[offset++];
            if (shift == 28 && (current & 0xf0) != 0)
                return false;

            value |= (uint)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
            {
                // Reject overlong encodings so the byte representation is deterministic.
                return offset - start == 1 || (current & 0x7f) != 0;
            }

            shift += 7;
        }

        return false;
    }

    private readonly struct SubtreeResult
    {
        private SubtreeResult(bool isUniform, uint value, int nodeOffset)
        {
            IsUniform = isUniform;
            Value = value;
            NodeOffset = nodeOffset;
        }

        internal bool IsUniform { get; }
        internal uint Value { get; }
        internal int NodeOffset { get; }

        internal static SubtreeResult Uniform(uint value) => new(true, value, default);
        internal static SubtreeResult Node(int offset) => new(false, default, offset);
    }

    private readonly struct BuildPlan
    {
        private BuildPlan(int encodedLength, StorageKind storageKind, uint uniformValue)
        {
            EncodedLength = encodedLength;
            StorageKind = storageKind;
            UniformValue = uniformValue;
        }

        internal int EncodedLength { get; }
        internal StorageKind StorageKind { get; }
        internal uint UniformValue { get; }

        internal static BuildPlan Uniform(int encodedLength, uint value) =>
            new(encodedLength, StorageKind.Uniform, value);

        internal static BuildPlan Tree(int encodedLength) =>
            new(encodedLength, StorageKind.Tree, default);

        internal static BuildPlan Dense(int encodedLength) =>
            new(encodedLength, StorageKind.Dense, default);
    }

    private ref struct OctreeWriter
    {
        private readonly Span<byte> _destination;
        private readonly bool _measureOnly;
        private readonly int _limit;

        internal OctreeWriter(Span<byte> destination, bool measureOnly, int limit = int.MaxValue)
        {
            _destination = destination;
            _measureOnly = measureOnly;
            _limit = measureOnly ? limit : destination.Length;
            Position = HeaderSize;
        }

        internal int Position { get; private set; }

        internal void Rewind(int position) => Position = position;

        internal bool WriteByte(byte value)
        {
            if (Position == int.MaxValue)
                return false;
            if ((uint)Position >= (uint)_limit)
                return false;
            if (!_measureOnly)
            {
                _destination[Position] = value;
            }

            Position++;
            return true;
        }

        internal bool WriteVarUInt(uint value)
        {
            while (value >= 0x80)
            {
                if (!WriteByte((byte)(value | 0x80)))
                    return false;
                value >>= 7;
            }

            return WriteByte((byte)value);
        }
    }
}

internal enum StorageKind : byte
{
    Tree = 0,
    Uniform = 1,
    Dense = 2
}
