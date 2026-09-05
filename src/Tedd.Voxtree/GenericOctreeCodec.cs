using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tedd.Voxtree;

/// <summary>
/// Version-2 codec over one exact-width primitive storage type. Public voxel structs
/// are reinterpreted only at the boundary by <see cref="VoxelCodec{T}"/>.
/// </summary>
internal static class GenericOctreeCodec<TStorage>
    where TStorage : unmanaged, IEquatable<TStorage>
{
    internal const byte Magic = 0x4f; // 'O'
    internal const int HeaderSize = 3;
    internal const int MinimumNodeSize = 1 + 8;

    private const int Version = 2;
    private const int VersionShift = 6;
    private const byte VersionMask = 0xc0;
    private const int LevelShift = 2;
    private const byte LevelMask = 0x3c;
    private const byte StorageKindMask = 0x03;
    private static readonly int ValueSize = Unsafe.SizeOf<TStorage>();
    private static readonly int MaximumNodeSize =
        1 + (8 * Math.Max(StorageInteger<TStorage>.MaximumVarIntBytes, 5));

    internal static int GetRequiredSize(ReadOnlySpan<TStorage> source, int levels, DenseVoxelLayout layout) =>
        CreateBuildPlan(source, levels, layout).EncodedLength;

    internal static byte[] BuildOwned(ReadOnlySpan<TStorage> source, int levels, DenseVoxelLayout layout)
    {
        var plan = CreateBuildPlan(source, levels, layout);
#if NET10_0_OR_GREATER
        var encoded = GC.AllocateUninitializedArray<byte>(plan.EncodedLength);
#else
        var encoded = new byte[plan.EncodedLength];
#endif

        if (plan.StorageKind == StorageKind.Uniform)
        {
            var writer = new OctreeWriter(encoded, measureOnly: false);
            if (!writer.WriteValue(plan.UniformValue) || writer.Position != encoded.Length)
                throw new InvalidOperationException("The octree encoder produced an inconsistent uniform result.");
            WriteHeader(encoded, levels, StorageKind.Uniform);
            return encoded;
        }

        if (plan.StorageKind == StorageKind.Dense)
        {
            WriteDense(source, encoded.AsSpan(HeaderSize), levels, layout);
            WriteHeader(encoded, levels, StorageKind.Dense);
            return encoded;
        }

        if (!TryBuild(source, levels, encoded, out var written, layout) || written != encoded.Length)
            throw new InvalidOperationException("The octree encoder produced an inconsistent tree result.");
        return encoded;
    }

    private static BuildPlan CreateBuildPlan(ReadOnlySpan<TStorage> source, int levels, DenseVoxelLayout layout)
    {
        ValidateSource(source, levels);
        DenseVoxel.Validate(layout);
        var denseLength = GetDenseSize(source.Length);
        var writer = new OctreeWriter(Span<byte>.Empty, measureOnly: true, denseLength);

        if (!BuildSubtree(source, levels, 0, 1 << levels, 1 << (levels * 2), ref writer, out var root, layout))
            return BuildPlan.Dense(denseLength);

        if (root.IsUniform)
        {
            if (!writer.WriteValue(root.Value))
                return BuildPlan.Dense(denseLength);
            return writer.Position < denseLength
                ? BuildPlan.Uniform(writer.Position, root.Value)
                : BuildPlan.Dense(denseLength);
        }

        var rootLength = writer.Position - root.NodeOffset;
        if (rootLength < MinimumNodeSize || rootLength > MaximumNodeSize || rootLength > byte.MaxValue)
            throw new InvalidOperationException("The octree encoder produced an invalid root node.");
        if (!writer.WriteByte((byte)rootLength))
            return BuildPlan.Dense(denseLength);

        return writer.Position < denseLength ? BuildPlan.Tree(writer.Position) : BuildPlan.Dense(denseLength);
    }

    internal static int GetMaximumSize(int levels)
    {
        OctreeCodec.ValidateLevels(levels);
        return GetDenseSize(OctreeCodec.GetVoxelCount(levels));
    }

    internal static bool TryBuild(ReadOnlySpan<TStorage> source, int levels, Span<byte> destination,
        out int bytesWritten, DenseVoxelLayout layout)
    {
        ValidateSource(source, levels);
        DenseVoxel.Validate(layout);
        if (MemoryMarshal.AsBytes(source).Overlaps(destination))
            throw new ArgumentException("Source and destination must not overlap.", nameof(destination));

        bytesWritten = 0;
        if (destination.Length < HeaderSize + 1)
            return false;

        var denseLength = GetDenseSize(source.Length);
        var writer = new OctreeWriter(destination[..Math.Min(destination.Length, denseLength)], measureOnly: false);
        var treeCompleted = BuildSubtree(source, levels, 0, 1 << levels, 1 << (levels * 2),
            ref writer, out var root, layout);

        if (treeCompleted && root.IsUniform)
        {
            if (!writer.WriteValue(root.Value))
                treeCompleted = false;
        }
        else if (treeCompleted)
        {
            var rootLength = writer.Position - root.NodeOffset;
            if (rootLength < MinimumNodeSize || rootLength > MaximumNodeSize || rootLength > byte.MaxValue)
                treeCompleted = false;
            else if (!writer.WriteByte((byte)rootLength))
                treeCompleted = false;
        }

        if (!treeCompleted || writer.Position >= denseLength)
        {
            if (destination.Length < denseLength)
                return false;
            WriteDense(source, destination[HeaderSize..denseLength], levels, layout);
            WriteHeader(destination, levels, StorageKind.Dense);
            bytesWritten = denseLength;
            return true;
        }

        WriteHeader(destination, levels, root.IsUniform ? StorageKind.Uniform : StorageKind.Tree);
        bytesWritten = writer.Position;
        return true;
    }

    internal static bool TryReadHeader(ReadOnlySpan<byte> data, out int levels, out StorageKind storageKind)
    {
        levels = default;
        storageKind = default;
        if (data.Length < HeaderSize + 1 || data[0] != Magic)
            return false;

        var metadata = data[1];
        if (((metadata & VersionMask) >> VersionShift) != Version || data[2] != GetSizeCode())
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
            return TryReadValue(data, ref cursor, out _) && cursor == data.Length;
        }
        if (storageKind == StorageKind.Dense)
            return data.Length == GetDenseSize(OctreeCodec.GetVoxelCount(levels));
        if (levels == 0 || data.Length < HeaderSize + MinimumNodeSize + 1)
            return false;

        var rootLength = data[^1];
        return rootLength >= MinimumNodeSize && rootLength <= MaximumNodeSize &&
               data.Length - 1 - rootLength >= HeaderSize;
    }

    internal static bool IsWellFormed(ReadOnlySpan<byte> data, int levels, StorageKind storageKind)
    {
        if (!TryReadHeader(data, out var encodedLevels, out var encodedKind) ||
            encodedLevels != levels || encodedKind != storageKind)
            return false;
        if (storageKind is StorageKind.Uniform or StorageKind.Dense)
            return true;

        var bodyEnd = data.Length - 1;
        var rootLength = data[^1];
        var rootOffset = bodyEnd - rootLength;
        return TryValidateNode(data[..bodyEnd], rootOffset, levels, out var subtreeStart, out var nodeEnd) &&
               subtreeStart == HeaderSize && nodeEnd == bodyEnd && nodeEnd - rootOffset == rootLength;
    }

    internal static bool TryGet(ReadOnlySpan<byte> data, int levels, StorageKind storageKind,
        int x, int y, int z, out TStorage value)
    {
        value = default;
        var sideLength = 1 << levels;
        if ((uint)x >= (uint)sideLength || (uint)y >= (uint)sideLength || (uint)z >= (uint)sideLength)
            return false;
        return TryGetUnchecked(data, levels, storageKind, x, y, z, out value);
    }

    internal static bool TryGetUnchecked(ReadOnlySpan<byte> data, int levels, StorageKind storageKind,
        int x, int y, int z, out TStorage value)
    {
        value = default;
        var sideLength = 1 << levels;
        if (storageKind == StorageKind.Uniform)
        {
            var cursor = HeaderSize;
            return TryReadValue(data, ref cursor, out value) && cursor == data.Length;
        }

        if (storageKind == StorageKind.Dense)
        {
            var flatIndex = (x * sideLength * sideLength) + (y * sideLength) + z;
            return TryReadFixedValue(data, HeaderSize + (flatIndex * ValueSize), out value);
        }

        if (levels == 0 || data.Length < HeaderSize + MinimumNodeSize + 1)
            return false;
        var body = data[..^1];
        var rootLength = data[^1];
        var nodeOffset = body.Length - rootLength;
        if (rootLength < MinimumNodeSize || rootLength > MaximumNodeSize || nodeOffset < HeaderSize)
            return false;

        for (var bit = levels - 1; bit >= 0; bit--)
        {
            var nodeStart = nodeOffset;
            if ((uint)nodeStart >= (uint)body.Length)
                return false;
            var cursor = nodeStart;
            var uniformMask = body[cursor++];
            var child = (((x >> bit) & 1) << 2) | (((y >> bit) & 1) << 1) | ((z >> bit) & 1);
            for (var index = 0; index < child; index++)
            {
                if ((uniformMask & (1 << index)) != 0)
                {
                    if (!SkipValue(body, ref cursor)) return false;
                }
                else if (!OctreeCodec.TryReadVarUInt(body, ref cursor, out _)) return false;
            }

            if ((uniformMask & (1 << child)) != 0)
                return TryReadValue(body, ref cursor, out value);
            if (!OctreeCodec.TryReadVarUInt(body, ref cursor, out var distance) ||
                bit == 0 || distance == 0 || distance > (uint)(nodeStart - HeaderSize))
                return false;
            nodeOffset = nodeStart - (int)distance;
        }
        return false;
    }

    internal static bool TryCopyTo(ReadOnlySpan<byte> data, int levels, StorageKind storageKind,
        Span<TStorage> destination)
    {
        var count = OctreeCodec.GetVoxelCount(levels);
        if (destination.Length < count || MemoryMarshal.AsBytes(destination[..count]).Overlaps(data) ||
            !IsWellFormed(data, levels, storageKind))
            return false;

        destination = destination[..count];
        if (storageKind == StorageKind.Uniform)
        {
            var cursor = HeaderSize;
            if (!TryReadValue(data, ref cursor, out var value) || cursor != data.Length) return false;
            destination.Fill(value);
            return true;
        }
        if (storageKind == StorageKind.Dense)
        {
            ReadDense(data[HeaderSize..], destination);
            return true;
        }
        var body = data[..^1];
        return TryDecodeNode(body, body.Length - data[^1], levels, 1 << levels, 0, 0, 0, destination);
    }

    internal static bool TryReadValue(ReadOnlySpan<byte> data, ref int offset, out TStorage value)
    {
        value = default;
        var start = offset;
        for (var shift = 0; shift < StorageInteger<TStorage>.BitCount; shift += 7)
        {
            if ((uint)offset >= (uint)data.Length) return false;
            var current = data[offset++];
            var payload = (byte)(current & 0x7f);
            if (!StorageInteger<TStorage>.TryAppendSevenBits(ref value, payload, shift)) return false;
            if ((current & 0x80) == 0)
                return offset - start == 1 || payload != 0;
        }
        return false;
    }

    internal static bool TryReadFixedValue(ReadOnlySpan<byte> data, int offset, out TStorage value)
    {
        value = default;
        if ((uint)offset > (uint)data.Length || data.Length - offset < ValueSize)
            return false;
        value = ReadLittleEndian(data.Slice(offset, ValueSize));
        return true;
    }

    private static bool SkipValue(ReadOnlySpan<byte> data, ref int offset)
    {
        return TryReadValue(data, ref offset, out _);
    }

    private static void ValidateSource(ReadOnlySpan<TStorage> source, int levels)
    {
        OctreeCodec.ValidateLevels(levels);
        var expected = OctreeCodec.GetVoxelCount(levels);
        if (source.Length != expected)
            throw new ArgumentException(
                $"Source must contain exactly {expected} elements for {levels} levels; it contains {source.Length}.",
                nameof(source));
    }

    private static int GetDenseSize(int valueCount) => checked(HeaderSize + (valueCount * ValueSize));

    private static void WriteHeader(Span<byte> destination, int levels, StorageKind storageKind)
    {
        destination[0] = Magic;
        destination[1] = (byte)((Version << VersionShift) | (levels << LevelShift) | (byte)storageKind);
        destination[2] = GetSizeCode();
    }

    private static byte GetSizeCode() => ValueSize switch
    {
        1 => 0, 2 => 1, 4 => 2, 8 => 3, 16 => 4,
        _ => throw new InvalidOperationException("Invalid generic codec storage size.")
    };

    private static void WriteDense(ReadOnlySpan<TStorage> source, Span<byte> destination,
        int levels, DenseVoxelLayout layout)
    {
        if (layout == DenseVoxelLayout.Morton)
        {
            var side = 1 << levels;
            for (var x = 0; x < side; x++)
            for (var y = 0; y < side; y++)
            for (var z = 0; z < side; z++)
                WriteLittleEndian(destination.Slice(((x * side + y) * side + z) * ValueSize, ValueSize),
                    source[DenseVoxel.Index(x, y, z, side, layout)]);
            return;
        }
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.AsBytes(source).CopyTo(destination);
            return;
        }
        for (var index = 0; index < source.Length; index++)
            WriteLittleEndian(destination.Slice(index * ValueSize, ValueSize), source[index]);
    }

    private static void ReadDense(ReadOnlySpan<byte> source, Span<TStorage> destination)
    {
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.Cast<byte, TStorage>(source).CopyTo(destination);
            return;
        }
        for (var index = 0; index < destination.Length; index++)
            destination[index] = ReadLittleEndian(source.Slice(index * ValueSize, ValueSize));
    }

    private static void WriteLittleEndian(Span<byte> destination, TStorage value)
    {
#if NETSTANDARD2_1
        MemoryMarshal.Write(destination, ref value);
#else
        MemoryMarshal.Write(destination, in value);
#endif
        if (!BitConverter.IsLittleEndian) Reverse(destination);
    }

    private static TStorage ReadLittleEndian(ReadOnlySpan<byte> source)
    {
        if (BitConverter.IsLittleEndian) return MemoryMarshal.Read<TStorage>(source);
        Span<byte> temporary = stackalloc byte[16];
        source.CopyTo(temporary);
        var bytes = temporary[..ValueSize];
        Reverse(bytes);
        return MemoryMarshal.Read<TStorage>(bytes);
    }

    private static void Reverse(Span<byte> bytes)
    {
        for (var left = 0; left < bytes.Length / 2; left++)
        {
            var right = bytes.Length - left - 1;
            (bytes[left], bytes[right]) = (bytes[right], bytes[left]);
        }
    }

    private static bool BuildSubtree(ReadOnlySpan<TStorage> source, int level, int baseIndex,
        int rowStride, int planeStride, ref OctreeWriter writer, out SubtreeResult result,
        DenseVoxelLayout layout)
    {
        if (level == 0)
        {
            result = SubtreeResult.Uniform(source[baseIndex]);
            return true;
        }

        if (level >= 2 && (layout == DenseVoxelLayout.Morton
            ? IsUniform(source.Slice(baseIndex, 1 << (level * 3)))
            : IsUniformRegion(source, baseIndex, 1 << level, rowStride, planeStride)))
        {
            result = SubtreeResult.Uniform(source[baseIndex]);
            return true;
        }

        var subtreeStart = writer.Position;
        Span<SubtreeResult> children = stackalloc SubtreeResult[8];
        var half = 1 << (level - 1);
        var allUniform = true;
        var firstValue = default(TStorage);

        for (var child = 0; child < 8; child++)
        {
            var childBase = layout == DenseVoxelLayout.Morton
                ? baseIndex + (((child >> 2) | (child & 2) | ((child & 1) << 2)) * half * half * half)
                : baseIndex + (((child >> 2) & 1) * half * planeStride) +
                  (((child >> 1) & 1) * half * rowStride) + ((child & 1) * half);
            if (!BuildSubtree(source, level - 1, childBase, rowStride, planeStride,
                    ref writer, out children[child], layout))
            {
                result = default;
                return false;
            }
            if (child == 0) firstValue = children[child].Value;
            if (!children[child].IsUniform || !children[child].Value.Equals(firstValue)) allUniform = false;
        }

        if (allUniform)
        {
            writer.Rewind(subtreeStart);
            result = SubtreeResult.Uniform(firstValue);
            return true;
        }

        byte uniformMask = 0;
        for (var child = 0; child < 8; child++)
            if (children[child].IsUniform) uniformMask |= (byte)(1 << child);

        var nodeOffset = writer.Position;
        if (!writer.WriteByte(uniformMask))
        {
            result = default;
            return false;
        }
        for (var child = 0; child < 8; child++)
        {
            var written = children[child].IsUniform
                ? writer.WriteValue(children[child].Value)
                : writer.WriteVarUInt((uint)(nodeOffset - children[child].NodeOffset));
            if (!written)
            {
                result = default;
                return false;
            }
        }
        result = SubtreeResult.Node(nodeOffset);
        return true;
    }

    private static bool IsUniform(ReadOnlySpan<TStorage> source)
    {
        var value = source[0];
        for (var index = 1; index < source.Length; index++)
            if (!source[index].Equals(value)) return false;
        return true;
    }

    private static bool IsUniformRegion(ReadOnlySpan<TStorage> source, int start, int size,
        int rowStride, int planeStride)
    {
        var value = source[start];
        if (size == rowStride) return IsUniform(source.Slice(start, size * size * size));
        for (var x = 0; x < size; x++)
        {
            var plane = start + x * planeStride;
            for (var y = 0; y < size; y++)
            {
                var row = source.Slice(plane + y * rowStride, size);
                for (var z = 0; z < row.Length; z++)
                    if (!row[z].Equals(value)) return false;
            }
        }
        return true;
    }

    private static bool TryValidateNode(ReadOnlySpan<byte> body, int nodeOffset, int level,
        out int subtreeStart, out int nodeEnd)
    {
        subtreeStart = default;
        nodeEnd = default;
        if (level <= 0 || (uint)nodeOffset >= (uint)body.Length) return false;

        var cursor = nodeOffset;
        var uniformMask = body[cursor++];
        Span<TStorage> values = stackalloc TStorage[8];
        Span<uint> distances = stackalloc uint[8];
        for (var child = 0; child < 8; child++)
        {
            if ((uniformMask & (1 << child)) != 0)
            {
                if (!TryReadValue(body, ref cursor, out values[child])) return false;
            }
            else if (!OctreeCodec.TryReadVarUInt(body, ref cursor, out distances[child])) return false;
        }

        nodeEnd = cursor;
        if (level == 1 && uniformMask != byte.MaxValue) return false;
        if (uniformMask == byte.MaxValue)
        {
            var collapsible = true;
            for (var child = 1; child < 8; child++) collapsible &= values[child].Equals(values[0]);
            if (collapsible) return false;
        }

        var expectedEnd = nodeOffset;
        for (var child = 7; child >= 0; child--)
        {
            if ((uniformMask & (1 << child)) != 0) continue;
            var distance = distances[child];
            if (distance == 0 || distance > (uint)(nodeOffset - HeaderSize)) return false;
            var childOffset = nodeOffset - (int)distance;
            if (!TryValidateNode(body, childOffset, level - 1, out var childStart, out var childEnd) ||
                childEnd != expectedEnd) return false;
            expectedEnd = childStart;
        }
        subtreeStart = expectedEnd;
        return true;
    }

    private static bool TryDecodeNode(ReadOnlySpan<byte> body, int nodeOffset, int level,
        int sideLength, int x, int y, int z, Span<TStorage> destination)
    {
        var cursor = nodeOffset;
        if ((uint)cursor >= (uint)body.Length) return false;
        var uniformMask = body[cursor++];
        Span<TStorage> values = stackalloc TStorage[8];
        Span<uint> distances = stackalloc uint[8];
        for (var child = 0; child < 8; child++)
        {
            if ((uniformMask & (1 << child)) != 0)
            {
                if (!TryReadValue(body, ref cursor, out values[child])) return false;
            }
            else if (!OctreeCodec.TryReadVarUInt(body, ref cursor, out distances[child])) return false;
        }

        var half = 1 << (level - 1);
        for (var child = 0; child < 8; child++)
        {
            var childX = x + (((child >> 2) & 1) * half);
            var childY = y + (((child >> 1) & 1) * half);
            var childZ = z + ((child & 1) * half);
            if ((uniformMask & (1 << child)) != 0)
            {
                FillRegion(destination, sideLength, childX, childY, childZ, half, values[child]);
                continue;
            }
            if (level == 1 || !TryDecodeNode(body, nodeOffset - (int)distances[child], level - 1,
                    sideLength, childX, childY, childZ, destination)) return false;
        }
        return true;
    }

    private static void FillRegion(Span<TStorage> destination, int sideLength,
        int x, int y, int z, int size, TStorage value)
    {
        var planeStride = sideLength * sideLength;
        for (var offsetX = 0; offsetX < size; offsetX++)
        {
            var plane = (x + offsetX) * planeStride;
            for (var offsetY = 0; offsetY < size; offsetY++)
                destination.Slice(plane + ((y + offsetY) * sideLength) + z, size).Fill(value);
        }
    }

    private readonly struct SubtreeResult
    {
        private SubtreeResult(bool isUniform, TStorage value, int nodeOffset)
        {
            IsUniform = isUniform;
            Value = value;
            NodeOffset = nodeOffset;
        }
        internal bool IsUniform { get; }
        internal TStorage Value { get; }
        internal int NodeOffset { get; }
        internal static SubtreeResult Uniform(TStorage value) => new(true, value, default);
        internal static SubtreeResult Node(int offset) => new(false, default, offset);
    }

    private readonly struct BuildPlan
    {
        private BuildPlan(int encodedLength, StorageKind storageKind, TStorage uniformValue)
        {
            EncodedLength = encodedLength;
            StorageKind = storageKind;
            UniformValue = uniformValue;
        }
        internal int EncodedLength { get; }
        internal StorageKind StorageKind { get; }
        internal TStorage UniformValue { get; }
        internal static BuildPlan Uniform(int encodedLength, TStorage value) => new(encodedLength, StorageKind.Uniform, value);
        internal static BuildPlan Tree(int encodedLength) => new(encodedLength, StorageKind.Tree, default);
        internal static BuildPlan Dense(int encodedLength) => new(encodedLength, StorageKind.Dense, default);
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
            if (Position == int.MaxValue || (uint)Position >= (uint)_limit) return false;
            if (!_measureOnly) _destination[Position] = value;
            Position++;
            return true;
        }

        internal bool WriteValue(TStorage value)
        {
            while (StorageInteger<TStorage>.HasMoreThanSevenBits(value))
            {
                if (!WriteByte((byte)(StorageInteger<TStorage>.LowByte(value) | 0x80))) return false;
                value = StorageInteger<TStorage>.ShiftRightSeven(value);
            }
            return WriteByte(StorageInteger<TStorage>.LowByte(value));
        }

        internal bool WriteVarUInt(uint value)
        {
            while (value >= 0x80)
            {
                if (!WriteByte((byte)(value | 0x80))) return false;
                value >>= 7;
            }
            return WriteByte((byte)value);
        }
    }
}
