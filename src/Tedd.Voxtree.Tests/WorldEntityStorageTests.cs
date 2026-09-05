using System.IO.Compression;

namespace Tedd.Voxtree.Tests;

public sealed class WorldEntityStorageTests
{
    public static IEnumerable<object[]> SupportedCompressionFormats() =>
        Enum.GetValues<ChunkCompression>()
            .Where(ChunkStorageOptions.IsCompressionSupported)
            .Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(SupportedCompressionFormats))]
    public void StoredChunksRoundTripWithEveryNativeCompressionFormat(ChunkCompression compression)
    {
        var directory = NewDirectory();
        try
        {
            var options = new ChunkStorageOptions(directory)
            {
                Compression = compression,
                CompressionLevel = CompressionLevel.Fastest
            };
            var world = new WorldEntity(4, 2, options);
            var coordinate = new ChunkCoordinate(-17, 23, -20);
            var source = PatternedChunk();
            world.SetChunk(coordinate, source);

            Assert.True(world.TryGetChunkResidencyInfo(coordinate, out var dirty));
            Assert.True(dirty.IsDirty);
            Assert.Equal(source.SerializedLength, dirty.EstimatedBytes);
            world.SaveChunk(coordinate);

            Assert.True(File.Exists(options.GetChunkPath(coordinate)));
            Assert.True(world.TryGetChunkResidencyInfo(coordinate, out var saved));
            Assert.False(saved.IsDirty);
            Assert.True(world.RemoveChunk(coordinate));

            // Loading selects the format stored in the file, not the current save preference.
            options.Compression = compression == ChunkCompression.Direct
                ? ChunkCompression.Brotli : ChunkCompression.Direct;
            var loaded = world.LoadChunkFromStorage(coordinate);

            Assert.Equal(71u, loaded.GetChannel(0).Get(1, 2, 3));
            Assert.Equal(93u, loaded.GetChannel(1).Get(3, 1, 2));
            Assert.Equal(71u, world.Get(0, -67, 94, -77));
            Assert.True(world.TryGetChunkResidencyInfo(coordinate, out var resident));
            Assert.False(resident.IsDirty);
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public void DefaultCompressionIsZstandardAndSupportMatchesTarget()
    {
        var options = new ChunkStorageOptions(NewDirectory());
        try
        {
            Assert.Equal(ChunkCompression.Zstandard, options.Compression);
#if NET11_0_OR_GREATER
            Assert.True(ChunkStorageOptions.IsCompressionSupported(ChunkCompression.Zstandard));
#else
            Assert.False(ChunkStorageOptions.IsCompressionSupported(ChunkCompression.Zstandard));
#endif
            Assert.False(ChunkStorageOptions.IsCompressionSupported((ChunkCompression)byte.MaxValue));
        }
        finally { DeleteDirectory(options.DirectoryPath); }
    }

    [Fact]
    public void CorruptOrMismatchedStoredChunksAreRejected()
    {
        var directory = NewDirectory();
        try
        {
            var options = DirectStorage(directory);
            var coordinate = new ChunkCoordinate(1, 2, 3);
            var source = new WorldEntity(2, 1, options);
            source.SetChunk(coordinate, UniformChunk(1, 17));
            source.SaveChunk(coordinate);
            var path = options.GetChunkPath(coordinate);
            var file = File.ReadAllBytes(path);
            file[^1] ^= 0x40;
            File.WriteAllBytes(path, file);
            source.RemoveChunk(coordinate);
            Assert.Throws<InvalidDataException>(() => source.LoadChunkFromStorage(coordinate));

            source.SetChunk(coordinate, UniformChunk(1, 17));
            source.SaveChunk(coordinate);
            var wrongSchema = new WorldEntity(4, 1, options);
            Assert.Throws<InvalidDataException>(() => wrongSchema.LoadChunkFromStorage(coordinate));
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public void AccessSequenceDrivesDirtyLruEvictionAndDynamicTargets()
    {
        var directory = NewDirectory();
        try
        {
            var options = DirectStorage(directory);
            var world = new WorldEntity(2, 1, options);
            var a = new ChunkCoordinate(0, 0, 0);
            var b = new ChunkCoordinate(1, 0, 0);
            var c = new ChunkCoordinate(2, 0, 0);
            var chunkA = UniformChunk(1, 1);
            var chunkB = UniformChunk(1, 2);
            var chunkC = UniformChunk(1, 3);
            world.SetChunk(a, chunkA);
            world.SetChunk(b, chunkB);
            world.SetChunk(c, chunkC);
            var bytes = chunkA.SerializedLength;
            Assert.Equal(3L * bytes, world.EstimatedResidentBytes);
            Assert.True(world.TryGetChunkResidencyInfo(a, out var before));

            Assert.True(world.TryGetChunk(a, out _));
            Assert.True(world.TryGetChunkResidencyInfo(a, out var after));
            Assert.True(after.LastAccessSequence > before.LastAccessSequence);
            world.MaxResidentBytes = 2L * bytes;

            Assert.Equal(1, world.TrimToMemoryTarget());
            Assert.True(world.TryGetChunk(a, out _));
            Assert.False(world.TryGetChunk(b, out _));
            Assert.True(world.TryGetChunk(c, out _));
            Assert.True(File.Exists(options.GetChunkPath(b)));
            Assert.Equal(2L * bytes, world.EstimatedResidentBytes);

            world.MaxResidentBytes = 0;
            Assert.Equal(2, world.TrimToMemoryTarget());
            Assert.Empty(world.Chunks);
            Assert.Equal(0, world.EstimatedResidentBytes);
            Assert.True(File.Exists(options.GetChunkPath(a)));
            Assert.True(File.Exists(options.GetChunkPath(c)));
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public void LoadingAutomaticallyTrimsOlderResidentsButProtectsTheRequestedChunk()
    {
        var directory = NewDirectory();
        try
        {
            var options = DirectStorage(directory);
            var writer = new WorldEntity(2, 1, options);
            var a = new ChunkCoordinate(-1, 0, 0);
            var b = new ChunkCoordinate(1, 0, 0);
            writer.SetChunk(a, UniformChunk(1, 4));
            writer.SetChunk(b, UniformChunk(1, 5));
            writer.SaveAllChunks();
            var bytes = writer.Chunks[a].SerializedLength;

            var reader = new WorldEntity(2, 1, options, maxResidentBytes: bytes);
            Assert.True(reader.TryGetOrLoadChunk(a, out _));
            Assert.True(reader.TryGetOrLoadChunk(b, out var loaded));
            Assert.True(reader.TryGetOrLoadChunk(b, out var cached));

            Assert.False(reader.TryGetChunk(a, out _));
            Assert.True(reader.TryGetChunk(b, out var resident));
            Assert.Same(loaded, resident);
            Assert.Same(loaded, cached);
            Assert.Equal(bytes, reader.EstimatedResidentBytes);
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public void GenericChunksPreserveExactValuesThroughStorage()
    {
        var directory = NewDirectory();
        try
        {
            var options = DirectStorage(directory);
            var world = new WorldEntity<ushort>(2, 1, options);
            var coordinate = new ChunkCoordinate(7, -8, 9);
            var values = Enumerable.Repeat((ushort)1234, 8).ToArray();
            world.SetChunk(coordinate, OctreeChunk<ushort>.FromDense(1, 1, values));
            world.SaveChunk(coordinate);
            world.Clear();

            var loaded = world.LoadChunkFromStorage(coordinate);

            Assert.Equal((ushort)1234, loaded.GetChannel(0).Get(1, 1, 1));
            Assert.Equal((ushort)1234, world.Get(0, 15, -15, 19));
            Assert.True(world.TryGetChunkResidencyInfo(coordinate, out var info));
            Assert.False(info.IsDirty);
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public void MissingStorageAndInvalidTargetsFailExplicitly()
    {
        var world = new WorldEntity(2, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.MaxResidentBytes = -1);
        Assert.False(world.TrySaveChunk(0, 0, 0));
        Assert.Throws<InvalidOperationException>(() => world.LoadChunkFromStorage(0, 0, 0));
        world.SetChunk(0, 0, 0, UniformChunk(1, 1));
        world.MaxResidentBytes = 0;
        Assert.Throws<InvalidOperationException>(() => world.TrimToMemoryTarget());
        Assert.Single(world.Chunks);
    }

    [Fact]
    public void ChangingStorageMarksResidentsDirtyBeforeEviction()
    {
        var firstDirectory = NewDirectory();
        var secondDirectory = NewDirectory();
        try
        {
            var coordinate = new ChunkCoordinate(4, 5, 6);
            var world = new WorldEntity(2, 1, DirectStorage(firstDirectory));
            world.SetChunk(coordinate, UniformChunk(1, 8));
            world.SaveChunk(coordinate);
            Assert.True(world.TryGetChunkResidencyInfo(coordinate, out var clean));
            Assert.False(clean.IsDirty);

            var replacementStorage = DirectStorage(secondDirectory);
            world.StorageOptions = replacementStorage;
            Assert.True(world.TryGetChunkResidencyInfo(coordinate, out var moved));
            Assert.True(moved.IsDirty);
            world.MaxResidentBytes = 0;

            Assert.Equal(1, world.TrimToMemoryTarget());
            Assert.True(File.Exists(replacementStorage.GetChunkPath(coordinate)));
        }
        finally
        {
            DeleteDirectory(firstDirectory);
            DeleteDirectory(secondDirectory);
        }
    }

    private static ChunkStorageOptions DirectStorage(string directory) => new(directory)
    {
        Compression = ChunkCompression.Direct
    };

    private static OctreeChunk PatternedChunk()
    {
        var values = new uint[128];
        values[(1 * 4 + 2) * 4 + 3] = 71;
        values[64 + (3 * 4 + 1) * 4 + 2] = 93;
        return OctreeChunk.FromDense(2, 2, values);
    }

    private static OctreeChunk UniformChunk(int levels, uint value) =>
        OctreeChunk.FromDense(levels, 1,
            Enumerable.Repeat(value, 1 << (levels * 3)).ToArray());

    private static string NewDirectory() =>
        Path.Combine(Path.GetTempPath(), "voxtree-storage-" + Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
