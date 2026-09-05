using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tedd.Voxtree;

/// <summary>
/// A stack-only, allocation-free view over a serialized octree.
/// </summary>
/// <remarks>
/// The supplied span must be the exact encoded slice. The view does not own, pin,
/// copy, or extend the lifetime of that memory.
/// </remarks>
public readonly ref partial struct OctreeSpan
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly int _levels;
    private readonly StorageKind _storageKind;

    /// <summary>Creates a view over an exact serialized octree slice.</summary>
    /// <exception cref="FormatException">The header is invalid or unsupported.</exception>
    public OctreeSpan(ReadOnlySpan<byte> data)
    {
        if (!OctreeCodec.TryReadHeader(data, out _levels, out _storageKind))
            throw new FormatException("The octree header is malformed or uses an unsupported format.");

        _data = data;
    }

    private OctreeSpan(
        ReadOnlySpan<byte> data,
        int levels,
        StorageKind storageKind)
    {
        _data = data;
        _levels = levels;
        _storageKind = storageKind;
    }

    internal static OctreeSpan CreateTrusted(
        ReadOnlySpan<byte> data,
        int levels,
        StorageKind storageKind) =>
        new(data, levels, storageKind);

    /// <summary>Gets whether this is a non-default view with a supported header.</summary>
    public bool IsValid => !_data.IsEmpty;

    /// <summary>Gets the depth encoded in the data.</summary>
    public int Levels => _levels;

    /// <summary>Gets the number of cells along one axis.</summary>
    public int SideLength => 1 << _levels;

    /// <summary>Gets the total represented voxel count.</summary>
    public int Count => OctreeCodec.GetVoxelCount(_levels);

    /// <summary>Gets the exact encoded byte count.</summary>
    public int EncodedLength => _data.Length;

    /// <summary>Gets the underlying encoded bytes without copying.</summary>
    public ReadOnlySpan<byte> Data => _data;

    /// <summary>Gets the value at the specified coordinates.</summary>
    public uint this[int x, int y, int z] => Get(x, y, z);

    /// <summary>Gets the value at the specified coordinates.</summary>
    /// <exception cref="InvalidOperationException">This is a default view.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is outside the volume.</exception>
    /// <exception cref="FormatException">The encoded structure is malformed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Get(int x, int y, int z)
    {
        if (!IsValid)
            return OctreeThrowHelper.ThrowInvalidView();
        if ((uint)x >= (uint)SideLength)
            return OctreeThrowHelper.ThrowCoordinate(nameof(x), x);
        if ((uint)y >= (uint)SideLength)
            return OctreeThrowHelper.ThrowCoordinate(nameof(y), y);
        if ((uint)z >= (uint)SideLength)
            return OctreeThrowHelper.ThrowCoordinate(nameof(z), z);

        if (!OctreeCodec.TryGetUnchecked(_data, _levels, _storageKind, x, y, z, out var value))
            return OctreeThrowHelper.ThrowMalformed();

        return value;
    }

    /// <summary>Attempts to get a value without throwing for a bad coordinate or malformed structure.</summary>
    public bool TryGet(int x, int y, int z, out uint value)
    {
        if (!IsValid)
        {
            value = default;
            return false;
        }

        return OctreeCodec.TryGet(_data, _levels, _storageKind, x, y, z, out value);
    }

    /// <summary>Returns whether all coordinates are inside the represented volume.</summary>
    public bool Contains(int x, int y, int z) =>
        IsValid &&
        (uint)x < (uint)SideLength &&
        (uint)y < (uint)SideLength &&
        (uint)z < (uint)SideLength;

    /// <summary>Expands the encoded volume into the beginning of <paramref name="destination"/>.</summary>
    /// <exception cref="InvalidOperationException">This is a default view.</exception>
    /// <exception cref="ArgumentException">The destination is too short or overlaps the encoded data.</exception>
    /// <exception cref="FormatException">The encoded structure is malformed.</exception>
    public void CopyTo(Span<uint> destination)
    {
        if (!IsValid)
            throw new InvalidOperationException("The octree view is not initialized.");
        if (destination.Length < Count)
            throw new ArgumentException($"Destination must contain at least {Count} elements.", nameof(destination));
        if (MemoryMarshal.AsBytes(destination[..Count]).Overlaps(_data))
            throw new ArgumentException("Destination must not overlap the encoded octree.", nameof(destination));
        if (!OctreeCodec.TryCopyTo(_data, _levels, _storageKind, destination))
            throw new FormatException("The octree data is malformed.");
    }

    /// <summary>Attempts to expand the encoded volume into a caller-provided span.</summary>
    public bool TryCopyTo(Span<uint> destination) =>
        IsValid &&
        destination.Length >= Count &&
        !MemoryMarshal.AsBytes(destination[..Count]).Overlaps(_data) &&
        OctreeCodec.TryCopyTo(_data, _levels, _storageKind, destination);

    /// <summary>Performs allocation-free structural and primitive-encoding validation.</summary>
    public bool IsWellFormed() =>
        IsValid &&
        OctreeCodec.IsWellFormed(_data, _levels, _storageKind);

    /// <summary>Attempts to create a view from a supported basic header.</summary>
    /// <remarks>Call <see cref="IsWellFormed"/> when accepting untrusted serialized data.</remarks>
    public static bool TryCreate(ReadOnlySpan<byte> data, out OctreeSpan octree)
    {
        if (!OctreeCodec.TryReadHeader(data, out var levels, out var storageKind))
        {
            octree = default;
            return false;
        }

        octree = new OctreeSpan(data, levels, storageKind);
        return true;
    }
}
