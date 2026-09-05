namespace Tedd.Voxtree;

public sealed partial class WorldEntity<T> where T : unmanaged
{
    private long? _maxResidentBytes;
    private ChunkStorageOptions? _storageOptions;

    /// <summary>Creates a disk-backed generic global chunk world.</summary>
    public WorldEntity(int chunkSize, int channelCount, ChunkStorageOptions storageOptions,
        long? maxResidentBytes = null) : this(chunkSize, channelCount)
    {
        StorageOptions = storageOptions ?? throw new ArgumentNullException(nameof(storageOptions));
        MaxResidentBytes = maxResidentBytes;
    }

    /// <summary>Storage used by load, save, and dirty-chunk eviction operations.</summary>
    public ChunkStorageOptions? StorageOptions
    {
        get => _storageOptions;
        set
        {
            if (ReferenceEquals(_storageOptions, value)) return;
            _storageOptions = value;
            _chunks.MarkAllDirty();
        }
    }

    /// <summary>
    /// Optional soft target for estimated resident chunk bytes. Assigning a value does not perform I/O;
    /// call <see cref="TrimToMemoryTarget"/> to enforce it immediately.
    /// </summary>
    public long? MaxResidentBytes
    {
        get => _maxResidentBytes;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _maxResidentBytes = value;
        }
    }

    /// <summary>Total serialized packet bytes attributed to resident chunks.</summary>
    public long EstimatedResidentBytes => _chunks.EstimatedBytes;

    /// <summary>Gets last-access, size, and dirty-state metadata without changing recency.</summary>
    public bool TryGetChunkResidencyInfo(ChunkCoordinate coordinate, out ChunkResidencyInfo info) =>
        _chunks.TryGetResidencyInfo(coordinate, out info);

    /// <summary>Gets last-access, size, and dirty-state metadata without changing recency.</summary>
    public bool TryGetChunkResidencyInfo(long chunkX, long chunkY, long chunkZ,
        out ChunkResidencyInfo info) =>
        TryGetChunkResidencyInfo(new ChunkCoordinate(chunkX, chunkY, chunkZ), out info);

    /// <summary>Saves a resident chunk. False means it is not resident.</summary>
    public bool TrySaveChunk(ChunkCoordinate coordinate)
    {
        if (!_chunks.TryPeekValue(coordinate, out var chunk)) return false;
        SaveChunkCore(coordinate, chunk!);
        return true;
    }

    /// <summary>Saves a resident chunk. False means it is not resident.</summary>
    public bool TrySaveChunk(long chunkX, long chunkY, long chunkZ) =>
        TrySaveChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ));

    /// <summary>Saves a resident chunk, throwing when it is not resident.</summary>
    public void SaveChunk(ChunkCoordinate coordinate)
    {
        if (!TrySaveChunk(coordinate)) throw new InvalidOperationException("The requested chunk is not resident.");
    }

    /// <summary>Saves a resident chunk, throwing when it is not resident.</summary>
    public void SaveChunk(long chunkX, long chunkY, long chunkZ) =>
        SaveChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ));

    /// <summary>Saves every currently resident chunk and returns the number saved.</summary>
    public int SaveAllChunks()
    {
        var saved = 0;
        foreach (var pair in _chunks.Entries)
        {
            SaveChunkCore(pair.Key, pair.Value);
            saved++;
        }
        return saved;
    }

    /// <summary>Loads and validates a stored chunk. False means no file exists.</summary>
    public bool TryLoadChunkFromStorage(ChunkCoordinate coordinate, out OctreeChunk<T>? chunk)
    {
        var storage = RequireStorage();
        byte[] packet;
        try { packet = ChunkFile.Load(storage, coordinate, GetMaximumPacketLength()); }
        catch (FileNotFoundException) { chunk = null; return false; }
        catch (DirectoryNotFoundException) { chunk = null; return false; }
        var loaded = OctreeChunk<T>.FromEncoded(packet);
        ValidateStoredSchema(loaded);
        _chunks.Set(coordinate, loaded, loaded.SerializedLength, isDirty: false);
        TrimToMemoryTargetCore(coordinate);
        chunk = loaded;
        return true;
    }

    /// <summary>Loads and validates a stored chunk. False means no file exists.</summary>
    public bool TryLoadChunkFromStorage(long chunkX, long chunkY, long chunkZ, out OctreeChunk<T>? chunk) =>
        TryLoadChunkFromStorage(new ChunkCoordinate(chunkX, chunkY, chunkZ), out chunk);

    /// <summary>Gets a resident chunk or loads it from storage. False means no file exists.</summary>
    public bool TryGetOrLoadChunk(ChunkCoordinate coordinate, out OctreeChunk<T>? chunk) =>
        TryGetChunk(coordinate, out chunk) || TryLoadChunkFromStorage(coordinate, out chunk);

    /// <summary>Gets a resident chunk or loads it from storage. False means no file exists.</summary>
    public bool TryGetOrLoadChunk(long chunkX, long chunkY, long chunkZ, out OctreeChunk<T>? chunk) =>
        TryGetOrLoadChunk(new ChunkCoordinate(chunkX, chunkY, chunkZ), out chunk);

    /// <summary>Loads a stored chunk, throwing when no file exists.</summary>
    public OctreeChunk<T> LoadChunkFromStorage(ChunkCoordinate coordinate)
    {
        if (TryLoadChunkFromStorage(coordinate, out var chunk)) return chunk!;
        throw new FileNotFoundException("The requested chunk is not stored.", RequireStorage().GetChunkPath(coordinate));
    }

    /// <summary>Loads a stored chunk, throwing when no file exists.</summary>
    public OctreeChunk<T> LoadChunkFromStorage(long chunkX, long chunkY, long chunkZ) =>
        LoadChunkFromStorage(new ChunkCoordinate(chunkX, chunkY, chunkZ));

    /// <summary>Saves dirty least-recently-used chunks and evicts until the soft target is met.</summary>
    public int TrimToMemoryTarget() => TrimToMemoryTargetCore(excluded: null);

    private int TrimToMemoryTargetCore(ChunkCoordinate? excluded)
    {
        if (!_maxResidentBytes.HasValue) return 0;
        var removed = 0;
        foreach (var candidate in _chunks.GetEvictionCandidates(excluded))
        {
            if (_chunks.EstimatedBytes <= _maxResidentBytes.Value) break;
            if (candidate.IsDirty) SaveChunkCore(candidate.Coordinate, candidate.Value);
            _chunks.Remove(candidate.Coordinate);
            removed++;
        }
        return removed;
    }

    private void SaveChunkCore(ChunkCoordinate coordinate, OctreeChunk<T> chunk)
    {
        var packet = new byte[chunk.SerializedLength];
        chunk.CopyEncodedTo(packet);
        ChunkFile.Save(RequireStorage(), coordinate, packet);
        _chunks.MarkClean(coordinate, chunk);
    }

    private ChunkStorageOptions RequireStorage() => StorageOptions ??
        throw new InvalidOperationException("Chunk storage is not configured.");

    private int GetMaximumPacketLength() => checked(12 + ChannelCount * (4 + Octree<T>.GetMaximumSize(ChunkShift)));

    private void ValidateStoredSchema(OctreeChunk<T> chunk)
    {
        if (chunk.SideLength != ChunkSize || chunk.ChannelCount != ChannelCount)
            throw new InvalidDataException("Stored chunk schema does not match the world entity.");
    }
}
