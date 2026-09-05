using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tedd.Voxtree;

/// <summary>Validates and dispatches supported voxel value representations.</summary>
internal static class VoxelType<T> where T : unmanaged
{
    internal static readonly int Size = Unsafe.SizeOf<T>();

    // The type initializer performs the size lookup once for each closed T. Object
    // construction performs only this cold-path validity check; reads and writes
    // dispatch on Unsafe.SizeOf<T>(), which the JIT folds for a closed generic type.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Validate()
    {
        if (Size is not (1 or 2 or 4 or 8 or 16))
            ThrowUnsupported();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnsupported() =>
        throw new NotSupportedException(
            $"Voxel type {typeof(T)} occupies {Size} bytes. Supported unmanaged sizes are 1, 2, 4, 8, and 16 bytes.");

    internal static byte SizeCode => Size switch
    {
        1 => 0,
        2 => 1,
        4 => 2,
        8 => 3,
        16 => 4,
        _ => throw new NotSupportedException()
    };
}

/// <summary>An exact 128-bit storage cell for targets without System.UInt128.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VoxelUInt128 : IEquatable<VoxelUInt128>
{
    internal ulong Low;
    internal ulong High;

    public readonly bool Equals(VoxelUInt128 other) => Low == other.Low && High == other.High;
    public override readonly bool Equals(object? obj) => obj is VoxelUInt128 other && Equals(other);
    public override readonly int GetHashCode() => Low.GetHashCode() ^ High.GetHashCode();
}

/// <summary>Routes a closed voxel type to an exact-width codec.</summary>
internal static class VoxelCodec<T> where T : unmanaged
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StorageKind GetStorageKindUnchecked(ReadOnlySpan<byte> data) =>
        (StorageKind)(data[1] & 0x03);

    internal static int GetRequiredSize(ReadOnlySpan<T> source, int levels, DenseVoxelLayout layout) =>
        Unsafe.SizeOf<T>() switch
        {
            1 => GenericOctreeCodec<byte>.GetRequiredSize(MemoryMarshal.Cast<T, byte>(source), levels, layout),
            2 => GenericOctreeCodec<ushort>.GetRequiredSize(MemoryMarshal.Cast<T, ushort>(source), levels, layout),
            4 => GenericOctreeCodec<uint>.GetRequiredSize(MemoryMarshal.Cast<T, uint>(source), levels, layout),
            8 => GenericOctreeCodec<ulong>.GetRequiredSize(MemoryMarshal.Cast<T, ulong>(source), levels, layout),
            16 => GenericOctreeCodec<VoxelUInt128>.GetRequiredSize(MemoryMarshal.Cast<T, VoxelUInt128>(source), levels, layout),
            _ => ThrowUnsupported<int>()
        };

    internal static byte[] BuildOwned(ReadOnlySpan<T> source, int levels, DenseVoxelLayout layout) =>
        Unsafe.SizeOf<T>() switch
        {
            1 => GenericOctreeCodec<byte>.BuildOwned(MemoryMarshal.Cast<T, byte>(source), levels, layout),
            2 => GenericOctreeCodec<ushort>.BuildOwned(MemoryMarshal.Cast<T, ushort>(source), levels, layout),
            4 => GenericOctreeCodec<uint>.BuildOwned(MemoryMarshal.Cast<T, uint>(source), levels, layout),
            8 => GenericOctreeCodec<ulong>.BuildOwned(MemoryMarshal.Cast<T, ulong>(source), levels, layout),
            16 => GenericOctreeCodec<VoxelUInt128>.BuildOwned(MemoryMarshal.Cast<T, VoxelUInt128>(source), levels, layout),
            _ => ThrowUnsupported<byte[]>()
        };

    internal static int GetMaximumSize(int levels) => Unsafe.SizeOf<T>() switch
    {
        1 => GenericOctreeCodec<byte>.GetMaximumSize(levels),
        2 => GenericOctreeCodec<ushort>.GetMaximumSize(levels),
        4 => GenericOctreeCodec<uint>.GetMaximumSize(levels),
        8 => GenericOctreeCodec<ulong>.GetMaximumSize(levels),
        16 => GenericOctreeCodec<VoxelUInt128>.GetMaximumSize(levels),
        _ => ThrowUnsupported<int>()
    };

    internal static bool TryBuild(ReadOnlySpan<T> source, int levels, Span<byte> destination,
        out int bytesWritten, DenseVoxelLayout layout)
    {
        switch (Unsafe.SizeOf<T>())
        {
            case 1:
                return GenericOctreeCodec<byte>.TryBuild(MemoryMarshal.Cast<T, byte>(source), levels, destination, out bytesWritten, layout);
            case 2:
                return GenericOctreeCodec<ushort>.TryBuild(MemoryMarshal.Cast<T, ushort>(source), levels, destination, out bytesWritten, layout);
            case 4:
                return GenericOctreeCodec<uint>.TryBuild(MemoryMarshal.Cast<T, uint>(source), levels, destination, out bytesWritten, layout);
            case 8:
                return GenericOctreeCodec<ulong>.TryBuild(MemoryMarshal.Cast<T, ulong>(source), levels, destination, out bytesWritten, layout);
            case 16:
                return GenericOctreeCodec<VoxelUInt128>.TryBuild(MemoryMarshal.Cast<T, VoxelUInt128>(source), levels, destination, out bytesWritten, layout);
            default:
                bytesWritten = 0;
                return ThrowUnsupported<bool>();
        }
    }

    internal static bool TryReadHeader(ReadOnlySpan<byte> data, out int levels, out StorageKind storageKind)
    {
        switch (Unsafe.SizeOf<T>())
        {
            case 1: return GenericOctreeCodec<byte>.TryReadHeader(data, out levels, out storageKind);
            case 2: return GenericOctreeCodec<ushort>.TryReadHeader(data, out levels, out storageKind);
            case 4: return GenericOctreeCodec<uint>.TryReadHeader(data, out levels, out storageKind);
            case 8: return GenericOctreeCodec<ulong>.TryReadHeader(data, out levels, out storageKind);
            case 16: return GenericOctreeCodec<VoxelUInt128>.TryReadHeader(data, out levels, out storageKind);
            default:
                levels = default;
                storageKind = default;
                return ThrowUnsupported<bool>();
        }
    }

    internal static bool IsWellFormed(ReadOnlySpan<byte> data, int levels, StorageKind storageKind) =>
        Unsafe.SizeOf<T>() switch
        {
            1 => GenericOctreeCodec<byte>.IsWellFormed(data, levels, storageKind),
            2 => GenericOctreeCodec<ushort>.IsWellFormed(data, levels, storageKind),
            4 => GenericOctreeCodec<uint>.IsWellFormed(data, levels, storageKind),
            8 => GenericOctreeCodec<ulong>.IsWellFormed(data, levels, storageKind),
            16 => GenericOctreeCodec<VoxelUInt128>.IsWellFormed(data, levels, storageKind),
            _ => ThrowUnsupported<bool>()
        };

    internal static bool TryGet(ReadOnlySpan<byte> data, int levels, StorageKind storageKind,
        int x, int y, int z, out T value) => TryGetCore(data, levels, storageKind, x, y, z, false, out value);

    internal static bool TryGetUnchecked(ReadOnlySpan<byte> data, int levels, StorageKind storageKind,
        int x, int y, int z, out T value) => TryGetCore(data, levels, storageKind, x, y, z, true, out value);

    private static bool TryGetCore(ReadOnlySpan<byte> data, int levels, StorageKind storageKind,
        int x, int y, int z, bool uncheckedCoordinates, out T value)
    {
        switch (Unsafe.SizeOf<T>())
        {
            case 1:
            {
                var ok = uncheckedCoordinates
                    ? GenericOctreeCodec<byte>.TryGetUnchecked(data, levels, storageKind, x, y, z, out var storage)
                    : GenericOctreeCodec<byte>.TryGet(data, levels, storageKind, x, y, z, out storage);
                value = Unsafe.As<byte, T>(ref storage);
                return ok;
            }
            case 2:
            {
                var ok = uncheckedCoordinates
                    ? GenericOctreeCodec<ushort>.TryGetUnchecked(data, levels, storageKind, x, y, z, out var storage)
                    : GenericOctreeCodec<ushort>.TryGet(data, levels, storageKind, x, y, z, out storage);
                value = Unsafe.As<ushort, T>(ref storage);
                return ok;
            }
            case 4:
            {
                var ok = uncheckedCoordinates
                    ? GenericOctreeCodec<uint>.TryGetUnchecked(data, levels, storageKind, x, y, z, out var storage)
                    : GenericOctreeCodec<uint>.TryGet(data, levels, storageKind, x, y, z, out storage);
                value = Unsafe.As<uint, T>(ref storage);
                return ok;
            }
            case 8:
            {
                var ok = uncheckedCoordinates
                    ? GenericOctreeCodec<ulong>.TryGetUnchecked(data, levels, storageKind, x, y, z, out var storage)
                    : GenericOctreeCodec<ulong>.TryGet(data, levels, storageKind, x, y, z, out storage);
                value = Unsafe.As<ulong, T>(ref storage);
                return ok;
            }
            case 16:
            {
                var ok = uncheckedCoordinates
                    ? GenericOctreeCodec<VoxelUInt128>.TryGetUnchecked(data, levels, storageKind, x, y, z, out var storage)
                    : GenericOctreeCodec<VoxelUInt128>.TryGet(data, levels, storageKind, x, y, z, out storage);
                value = Unsafe.As<VoxelUInt128, T>(ref storage);
                return ok;
            }
            default:
                value = default;
                return ThrowUnsupported<bool>();
        }
    }

    internal static bool TryCopyTo(ReadOnlySpan<byte> data, int levels, StorageKind storageKind, Span<T> destination) =>
        Unsafe.SizeOf<T>() switch
        {
            1 => GenericOctreeCodec<byte>.TryCopyTo(data, levels, storageKind, MemoryMarshal.Cast<T, byte>(destination)),
            2 => GenericOctreeCodec<ushort>.TryCopyTo(data, levels, storageKind, MemoryMarshal.Cast<T, ushort>(destination)),
            4 => GenericOctreeCodec<uint>.TryCopyTo(data, levels, storageKind, MemoryMarshal.Cast<T, uint>(destination)),
            8 => GenericOctreeCodec<ulong>.TryCopyTo(data, levels, storageKind, MemoryMarshal.Cast<T, ulong>(destination)),
            16 => GenericOctreeCodec<VoxelUInt128>.TryCopyTo(data, levels, storageKind, MemoryMarshal.Cast<T, VoxelUInt128>(destination)),
            _ => ThrowUnsupported<bool>()
        };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TResult ThrowUnsupported<TResult>()
    {
        VoxelType<T>.Validate();
        throw new UnreachableException();
    }
}

// Avoid a System.Diagnostics dependency on older targets.
internal sealed class UnreachableException : Exception { }
