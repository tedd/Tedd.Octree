using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Tedd.Voxtree;

/// <summary>
/// Owns the compact, immutable encoding of a cubic <see cref="uint"/> voxel volume.
/// </summary>
/// <remarks>
/// Use <see cref="OctreeSpan"/> and the caller-buffer build APIs when the operation
/// must not allocate. An owned build allocates exactly one byte array for the result.
/// Published data is immutable, so concurrent readers are safe.
/// </remarks>
public sealed partial class Octree
{
    /// <summary>The largest supported depth.</summary>
    public const int MaxLevels = 9;

    /// <summary>The current serialized format version.</summary>
    public const int FormatVersion = 1;

    private readonly int _levels;
    private byte[] _data = Array.Empty<byte>();

    /// <summary>Creates an unbuilt tree with the specified depth.</summary>
    /// <param name="levels">Depth in the inclusive range 0 through <see cref="MaxLevels"/>.</param>
    public Octree(int levels)
    {
        OctreeCodec.ValidateLevels(levels);
        _levels = levels;
    }

    /// <summary>Creates and builds an owned tree.</summary>
    /// <param name="levels">Depth in the inclusive range 0 through <see cref="MaxLevels"/>.</param>
    /// <param name="values">Exactly <c>(2^levels)^3</c> values in X/Y/Z-major order.</param>
    public Octree(int levels, ReadOnlySpan<uint> values)
        : this(levels)
    {
        Build(values);
    }

    /// <summary>Gets whether this instance contains a completed build.</summary>
    public bool IsBuilt => Volatile.Read(ref _data).Length != 0;

    /// <summary>Gets the tree depth.</summary>
    public int Levels => _levels;

    /// <summary>Gets the number of cells along one axis.</summary>
    public int SideLength => 1 << _levels;

    /// <summary>Gets the total number of voxels represented by the tree.</summary>
    public int Count => OctreeCodec.GetVoxelCount(_levels);

    /// <summary>Gets the used encoded byte count, or zero before the first build.</summary>
    public int EncodedLength => Volatile.Read(ref _data).Length;

    /// <summary>Gets the immutable encoded bytes, or an empty memory before the first build.</summary>
    public ReadOnlyMemory<byte> Data => Volatile.Read(ref _data);

    /// <summary>Gets the value at the specified coordinates.</summary>
    public uint this[int x, int y, int z] => Get(x, y, z);

    /// <summary>
    /// Builds this instance from a dense volume, replacing the preceding build only after
    /// the new encoding has completed successfully.
    /// </summary>
    /// <remarks>This convenience API allocates one byte array of the exact encoded size.</remarks>
    public void Build(ReadOnlySpan<uint> values)
    {
        var encoded = OctreeCodec.BuildOwned(values, _levels);
        Volatile.Write(ref _data, encoded);
    }

    /// <summary>Gets the value at the specified coordinates.</summary>
    /// <exception cref="InvalidOperationException">The tree has not been built.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is outside the volume.</exception>
    /// <exception cref="FormatException">The encoded data is malformed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Get(int x, int y, int z)
    {
        var encoded = Volatile.Read(ref _data);
        if (encoded.Length == 0)
            return OctreeThrowHelper.ThrowUnbuilt();
        if ((uint)x >= (uint)SideLength)
            return OctreeThrowHelper.ThrowCoordinate(nameof(x), x);
        if ((uint)y >= (uint)SideLength)
            return OctreeThrowHelper.ThrowCoordinate(nameof(y), y);
        if ((uint)z >= (uint)SideLength)
            return OctreeThrowHelper.ThrowCoordinate(nameof(z), z);

        if (!OctreeCodec.TryGetUnchecked(
                encoded,
                _levels,
                OctreeCodec.GetStorageKindUnchecked(encoded),
                x,
                y,
                z,
                out var value))
        {
            return OctreeThrowHelper.ThrowMalformed();
        }

        return value;
    }

    /// <summary>Attempts to get a value without throwing for an unbuilt tree, bad coordinate, or malformed data.</summary>
    public bool TryGet(int x, int y, int z, out uint value)
    {
        var encoded = Volatile.Read(ref _data);
        if (encoded.Length == 0)
        {
            value = default;
            return false;
        }

        return OctreeCodec.TryGet(
            encoded,
            _levels,
            OctreeCodec.GetStorageKindUnchecked(encoded),
            x,
            y,
            z,
            out value);
    }

    /// <summary>Returns whether all coordinates are inside the represented volume.</summary>
    public bool Contains(int x, int y, int z) =>
        (uint)x < (uint)SideLength &&
        (uint)y < (uint)SideLength &&
        (uint)z < (uint)SideLength;

    /// <summary>Expands the encoded volume into the beginning of <paramref name="destination"/>.</summary>
    /// <exception cref="InvalidOperationException">The tree has not been built.</exception>
    /// <exception cref="ArgumentException">The destination is too short or overlaps the encoded data.</exception>
    /// <exception cref="FormatException">The encoded structure is malformed.</exception>
    public void CopyTo(Span<uint> destination)
    {
        AsSpan().CopyTo(destination);
    }

    /// <summary>Attempts to expand the encoded volume into a caller-provided span.</summary>
    public bool TryCopyTo(Span<uint> destination)
    {
        var encoded = Volatile.Read(ref _data);
        return encoded.Length != 0 &&
               OctreeSpan.CreateTrusted(encoded, _levels, OctreeCodec.GetStorageKindUnchecked(encoded))
                   .TryCopyTo(destination);
    }

    /// <summary>Creates an allocation-free, stack-only view over the owned encoding.</summary>
    /// <exception cref="InvalidOperationException">The tree has not been built.</exception>
    public OctreeSpan AsSpan()
    {
        var encoded = Volatile.Read(ref _data);
        if (encoded.Length == 0)
            throw new InvalidOperationException("The octree has not been built.");
        return OctreeSpan.CreateTrusted(encoded, _levels, OctreeCodec.GetStorageKindUnchecked(encoded));
    }

    /// <summary>Computes the exact destination size for an allocation-free build.</summary>
    public static int GetRequiredSize(ReadOnlySpan<uint> source, int levels) =>
        OctreeCodec.GetRequiredSize(source, levels);

    /// <summary>
    /// Gets a value-independent capacity that can hold any encoding at the specified depth.
    /// </summary>
    /// <remarks>
    /// This avoids a sizing pass when one destination is reused for multiple builds.
    /// The exact encoding remains <c>destination[..bytesWritten]</c>.
    /// </remarks>
    public static int GetMaximumSize(int levels) => OctreeCodec.GetMaximumSize(levels);

    /// <summary>Builds into caller-owned memory and returns the used byte count.</summary>
    /// <exception cref="ArgumentException">The source length is invalid, the destination is too short, or the spans overlap.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="levels"/> is outside the supported range.</exception>
    public static int Build(ReadOnlySpan<uint> source, int levels, Span<byte> destination)
    {
        if (TryBuild(source, levels, destination, out var bytesWritten))
            return bytesWritten;

        var required = GetRequiredSize(source, levels);
        throw new ArgumentException(
            $"Destination must contain at least {required} bytes; it contains {destination.Length}.",
            nameof(destination));
    }

    /// <summary>
    /// Attempts to build into caller-owned memory without allocating.
    /// </summary>
    /// <remarks>
    /// On success, only <c>destination[..bytesWritten]</c> is part of the encoding.
    /// On insufficient capacity this method returns <see langword="false"/> and sets
    /// <paramref name="bytesWritten"/> to zero; destination contents are then unspecified.
    /// </remarks>
    /// <exception cref="ArgumentException">The source length is invalid or the spans overlap.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="levels"/> is outside the supported range.</exception>
    public static bool TryBuild(
        ReadOnlySpan<uint> source,
        int levels,
        Span<byte> destination,
        out int bytesWritten) =>
        OctreeCodec.TryBuild(source, levels, destination, out bytesWritten);

    /// <summary>Copies and validates an existing serialized octree into an owned instance.</summary>
    /// <exception cref="FormatException">The encoded data is malformed or unsupported.</exception>
    public static Octree FromEncoded(ReadOnlySpan<byte> encoded)
    {
        var copy = encoded.ToArray();
        if (!OctreeSpan.TryCreate(copy, out var view) || !view.IsWellFormed())
            throw new FormatException("The octree data is malformed or uses an unsupported format.");

        var result = new Octree(view.Levels);
        Volatile.Write(ref result._data, copy);
        return result;
    }
}
