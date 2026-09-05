using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;

namespace Tedd.Voxtree;

/// <summary>Compression applied around an already encoded chunk packet.</summary>
public enum ChunkCompression : byte
{
    /// <summary>Store the octree packet directly.</summary>
    Direct = 0,
    /// <summary>Brotli compression.</summary>
    Brotli = 1,
    /// <summary>Raw Deflate compression.</summary>
    Deflate = 2,
    /// <summary>GZip compression.</summary>
    GZip = 3,
    /// <summary>ZLib compression.</summary>
    ZLib = 4,
    /// <summary>Zstandard compression; natively available on .NET 11 and later.</summary>
    Zstandard = 5
}

/// <summary>Configures disk persistence for an unbounded chunk world.</summary>
public sealed class ChunkStorageOptions
{
    /// <summary>Creates storage rooted at a filesystem directory.</summary>
    public ChunkStorageOptions(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("A storage directory is required.", nameof(directoryPath));
        DirectoryPath = Path.GetFullPath(directoryPath);
    }

    /// <summary>The absolute storage root.</summary>
    public string DirectoryPath { get; }
    /// <summary>The format used by subsequent saves. Files identify their own format when loaded.</summary>
    public ChunkCompression Compression { get; set; } = ChunkCompression.Zstandard;
    /// <summary>The compression tradeoff used by subsequent saves.</summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    /// <summary>Returns the sharded path for a chunk coordinate.</summary>
    public string GetChunkPath(ChunkCoordinate coordinate) => ChunkFile.GetPath(this, coordinate);

    /// <summary>Reports whether the current target framework natively supports a format.</summary>
    public static bool IsCompressionSupported(ChunkCompression compression) => compression switch
    {
        ChunkCompression.Direct or ChunkCompression.Brotli or ChunkCompression.Deflate or
            ChunkCompression.GZip => true,
#if NET6_0_OR_GREATER
        ChunkCompression.ZLib => true,
#else
        ChunkCompression.ZLib => false,
#endif
#if NET11_0_OR_GREATER
        ChunkCompression.Zstandard => true,
#else
        ChunkCompression.Zstandard => false,
#endif
        _ => false
    };
}

/// <summary>Residency and replacement metadata for one loaded chunk.</summary>
public readonly struct ChunkResidencyInfo
{
    internal ChunkResidencyInfo(ChunkCoordinate coordinate, long estimatedBytes,
        ulong lastAccessSequence, bool isDirty)
    {
        Coordinate = coordinate;
        EstimatedBytes = estimatedBytes;
        LastAccessSequence = lastAccessSequence;
        IsDirty = isDirty;
    }

    /// <summary>The chunk coordinate.</summary>
    public ChunkCoordinate Coordinate { get; }
    /// <summary>The encoded packet bytes attributed to this resident chunk.</summary>
    public long EstimatedBytes { get; }
    /// <summary>A world-local monotonic timestamp; larger values were used more recently.</summary>
    public ulong LastAccessSequence { get; }
    /// <summary>Whether the resident snapshot has changed since it was loaded or saved.</summary>
    public bool IsDirty { get; }
}

internal static class ChunkFile
{
    private const int HeaderSize = 44;
    private const byte Version = 1;
    private static readonly uint[] Crc32Table = CreateCrc32Table();

    internal static string GetPath(ChunkStorageOptions options, ChunkCoordinate coordinate)
    {
        var xShard = ((byte)coordinate.X).ToString("x2", CultureInfo.InvariantCulture);
        var zShard = ((byte)coordinate.Z).ToString("x2", CultureInfo.InvariantCulture);
        var name = string.Concat(
            coordinate.X.ToString(CultureInfo.InvariantCulture), "_",
            coordinate.Y.ToString(CultureInfo.InvariantCulture), "_",
            coordinate.Z.ToString(CultureInfo.InvariantCulture), ".vxc");
        return Path.Combine(options.DirectoryPath, xShard, zShard, name);
    }

    internal static void Save(ChunkStorageOptions options, ChunkCoordinate coordinate,
        ReadOnlySpan<byte> packet)
    {
        ValidateCompression(options.Compression, storedValue: false);
        var payload = ChunkCompressionCodec.Compress(packet, options.Compression, options.CompressionLevel);
        var file = new byte[checked(HeaderSize + payload.Length)];
        file[0] = (byte)'T'; file[1] = (byte)'V'; file[2] = (byte)'C'; file[3] = (byte)'F';
        file[4] = Version; file[5] = (byte)options.Compression;
        file[6] = 0; file[7] = HeaderSize;
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(8), packet.Length);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(12), payload.Length);
        BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(16), coordinate.X);
        BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(24), coordinate.Y);
        BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(32), coordinate.Z);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(40), ComputeCrc32(packet));
        payload.CopyTo(file, HeaderSize);
        WriteAtomically(GetPath(options, coordinate), file);
    }

    internal static byte[] Load(ChunkStorageOptions options, ChunkCoordinate coordinate, int maximumPacketLength)
    {
        var path = GetPath(options, coordinate);
        var file = File.ReadAllBytes(path);
        if (file.Length < HeaderSize || file[0] != 'T' || file[1] != 'V' ||
            file[2] != 'C' || file[3] != 'F' || file[4] != Version ||
            file[6] != 0 || file[7] != HeaderSize)
            throw new InvalidDataException("Invalid chunk-file header.");

        var compression = (ChunkCompression)file[5];
        ValidateCompression(compression, storedValue: true);
        var packetLength = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(8));
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(12));
        if (packetLength <= 0 || packetLength > maximumPacketLength || payloadLength < 0 ||
            payloadLength != file.Length - HeaderSize)
            throw new InvalidDataException("Invalid chunk-file lengths.");
        if (BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(16)) != coordinate.X ||
            BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(24)) != coordinate.Y ||
            BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(32)) != coordinate.Z)
            throw new InvalidDataException("Chunk-file coordinates do not match its path.");

        var packet = ChunkCompressionCodec.Decompress(file, HeaderSize, payloadLength,
            packetLength, compression);
        if (ComputeCrc32(packet) != BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)))
            throw new InvalidDataException("Chunk-file checksum mismatch.");
        return packet;
    }

    private static void ValidateCompression(ChunkCompression compression, bool storedValue)
    {
        if (!Enum.IsDefined(typeof(ChunkCompression), compression))
        {
            if (storedValue) throw new InvalidDataException("Unknown chunk compression format.");
            throw new ArgumentOutOfRangeException(nameof(compression));
        }
        if (!ChunkStorageOptions.IsCompressionSupported(compression))
            throw new PlatformNotSupportedException($"{compression} compression is unavailable on this target framework.");
    }

    private static void WriteAtomically(string path, byte[] data)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data) crc = (crc >> 8) ^ Crc32Table[(byte)(crc ^ value)];
        return ~crc;
    }

    private static uint[] CreateCrc32Table()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
                value = (value >> 1) ^ (0xedb88320u & (uint)-(int)(value & 1));
            table[index] = value;
        }
        return table;
    }
}
