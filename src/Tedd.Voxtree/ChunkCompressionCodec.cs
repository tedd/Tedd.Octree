using System.IO.Compression;

namespace Tedd.Voxtree;

internal static class ChunkCompressionCodec
{
    internal static byte[] Compress(ReadOnlySpan<byte> source, ChunkCompression compression,
        CompressionLevel level)
    {
        if (compression == ChunkCompression.Direct) return source.ToArray();
        using var output = new MemoryStream();
        using (var encoder = CreateEncoder(output, compression, level)) encoder.Write(source);
        return output.ToArray();
    }

    internal static byte[] Decompress(byte[] source, int offset, int length, int expectedLength,
        ChunkCompression compression)
    {
        var output = new byte[expectedLength];
        if (compression == ChunkCompression.Direct)
        {
            if (length != expectedLength) throw new InvalidDataException("Direct chunk payload has an invalid length.");
            Buffer.BlockCopy(source, offset, output, 0, length);
            return output;
        }

        using var input = new MemoryStream(source, offset, length, writable: false, publiclyVisible: true);
        using var decoder = CreateDecoder(input, compression);
        var written = 0;
        while (written < output.Length)
        {
            var read = decoder.Read(output, written, output.Length - written);
            if (read == 0) break;
            written += read;
        }
        if (written != output.Length || decoder.ReadByte() != -1)
            throw new InvalidDataException("Decompressed chunk length does not match its header.");
        return output;
    }

    private static Stream CreateEncoder(Stream output, ChunkCompression compression,
        CompressionLevel level)
    {
        switch (compression)
        {
            case ChunkCompression.Brotli:
                return new BrotliStream(output, level, leaveOpen: true);
            case ChunkCompression.Deflate:
                return new DeflateStream(output, level, leaveOpen: true);
            case ChunkCompression.GZip:
                return new GZipStream(output, level, leaveOpen: true);
#if NET6_0_OR_GREATER
            case ChunkCompression.ZLib:
                return new ZLibStream(output, level, leaveOpen: true);
#endif
#if NET11_0_OR_GREATER
            case ChunkCompression.Zstandard:
                return new ZstandardStream(output, level, leaveOpen: true);
#endif
            default:
                throw new PlatformNotSupportedException($"{compression} compression is unavailable on this target framework.");
        }
    }

    private static Stream CreateDecoder(Stream input, ChunkCompression compression)
    {
        switch (compression)
        {
            case ChunkCompression.Brotli:
                return new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true);
            case ChunkCompression.Deflate:
                return new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
            case ChunkCompression.GZip:
                return new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
#if NET6_0_OR_GREATER
            case ChunkCompression.ZLib:
                return new ZLibStream(input, CompressionMode.Decompress, leaveOpen: true);
#endif
#if NET11_0_OR_GREATER
            case ChunkCompression.Zstandard:
                return new ZstandardStream(input, CompressionMode.Decompress, leaveOpen: true);
#endif
            default:
                throw new PlatformNotSupportedException($"{compression} compression is unavailable on this target framework.");
        }
    }
}
