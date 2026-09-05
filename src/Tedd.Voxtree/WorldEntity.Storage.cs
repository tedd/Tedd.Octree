namespace Tedd.Voxtree;

public sealed partial class WorldEntity
{
    private long? _maxResidentBytes;
    private ChunkStorageOptions? _storageOptions;

    /// <summary>Creates a disk-backed global chunk world.</summary>
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

    /// <summary>Gets base-layer residency metadata without changing recency.</summary>
    public bool TryGetChunkResidencyInfo(ChunkCoordinate coordinate, out ChunkResidencyInfo info) =>
        TryGetChunkResidencyInfo(new ChunkAddress(0, coordinate), out info);

    /// <summary>Gets base-layer residency metadata without changing recency.</summary>
    public bool TryGetChunkResidencyInfo(long chunkX, long chunkY, long chunkZ,
        out ChunkResidencyInfo info) =>
        TryGetChunkResidencyInfo(new ChunkAddress(0, chunkX, chunkY, chunkZ), out info);

    /// <summary>Gets base or LOD residency metadata without changing recency.</summary>
    public bool TryGetChunkResidencyInfo(ChunkAddress address, out ChunkResidencyInfo info)
    {
        ValidateChunkAddress(address);
        return _chunks.TryGetResidencyInfo(address, out info);
    }

    /// <summary>Saves a resident base-layer chunk. False means it is not resident.</summary>
    public bool TrySaveChunk(ChunkCoordinate coordinate) =>
        TrySaveChunk(new ChunkAddress(0, coordinate));

    /// <summary>Saves a resident base-layer chunk. False means it is not resident.</summary>
    public bool TrySaveChunk(long chunkX, long chunkY, long chunkZ) =>
        TrySaveChunk(new ChunkAddress(0, chunkX, chunkY, chunkZ));

    /// <summary>Saves a resident base or LOD chunk. False means it is not resident.</summary>
    public bool TrySaveChunk(ChunkAddress address)
    {
        ValidateChunkAddress(address);
        if (!_chunks.TryPeekValue(address, out var chunk)) return false;
        SaveChunkCore(address, chunk!);
        return true;
    }

    /// <summary>Saves a resident base-layer chunk, throwing when it is not resident.</summary>
    public void SaveChunk(ChunkCoordinate coordinate) =>
        SaveChunk(new ChunkAddress(0, coordinate));

    /// <summary>Saves a resident base-layer chunk, throwing when it is not resident.</summary>
    public void SaveChunk(long chunkX, long chunkY, long chunkZ) =>
        SaveChunk(new ChunkAddress(0, chunkX, chunkY, chunkZ));

    /// <summary>Saves a resident base or LOD chunk, throwing when it is not resident.</summary>
    public void SaveChunk(ChunkAddress address)
    {
        if (!TrySaveChunk(address)) throw new InvalidOperationException("The requested chunk is not resident.");
    }

    /// <summary>Saves every dirty resident chunk and returns the number written.</summary>
    public int SaveAllChunks()
    {
        var saved = 0;
        foreach (var pair in _chunks.Entries)
        {
            if (!_chunks.TryPeekEntry(pair.Key, out _, out var isDirty) || !isDirty) continue;
            SaveChunkCore(pair.Key, pair.Value);
            saved++;
        }
        return saved;
    }

    /// <summary>Loads and validates a stored base-layer chunk. False means no file exists.</summary>
    public bool TryLoadChunkFromStorage(ChunkCoordinate coordinate, out OctreeChunk? chunk) =>
        TryLoadChunkFromStorage(new ChunkAddress(0, coordinate), out chunk);

    /// <summary>Loads and validates a stored base-layer chunk. False means no file exists.</summary>
    public bool TryLoadChunkFromStorage(long chunkX, long chunkY, long chunkZ, out OctreeChunk? chunk) =>
        TryLoadChunkFromStorage(new ChunkAddress(0, chunkX, chunkY, chunkZ), out chunk);

    /// <summary>Loads and validates a stored base or LOD chunk. False means no file exists.</summary>
    public bool TryLoadChunkFromStorage(ChunkAddress address, out OctreeChunk? chunk)
    {
        ValidateChunkAddress(address);
        var storage = RequireStorage();
        byte[] packet;
        try { packet = ChunkFile.Load(storage, address, GetMaximumPacketLength()); }
        catch (FileNotFoundException) { chunk = null; return false; }
        catch (DirectoryNotFoundException) { chunk = null; return false; }
        var loaded = OctreeChunk.FromEncoded(packet);
        ValidateStoredSchema(loaded);
        _chunks.Set(address, loaded, loaded.SerializedLength, isDirty: false);
        TrimToMemoryTargetCore(address);
        chunk = loaded;
        return true;
    }

    /// <summary>Gets, loads, or materializes a clean zero base-layer chunk.</summary>
    public bool TryGetOrLoadChunk(ChunkCoordinate coordinate, out OctreeChunk? chunk) =>
        TryGetOrLoadChunk(new ChunkAddress(0, coordinate), out chunk);

    /// <summary>Gets, loads, or materializes a clean zero base-layer chunk.</summary>
    public bool TryGetOrLoadChunk(long chunkX, long chunkY, long chunkZ, out OctreeChunk? chunk) =>
        TryGetOrLoadChunk(new ChunkAddress(0, chunkX, chunkY, chunkZ), out chunk);

    /// <summary>Gets, loads, or materializes a clean zero base or LOD chunk.</summary>
    public bool TryGetOrLoadChunk(ChunkAddress address, out OctreeChunk? chunk)
    {
        ValidateChunkAddress(address);
        if (TryGetChunk(address, out chunk) || TryLoadChunkFromStorage(address, out chunk)) return true;
        var empty = OctreeChunk.Empty(ChunkShift, ChannelCount);
        _chunks.Set(address, empty, empty.SerializedLength, isDirty: false, isImplicitEmpty: true);
        TrimToMemoryTargetCore(address);
        chunk = empty;
        return true;
    }

    /// <summary>Gets, loads, or materializes a clean zero base or LOD chunk.</summary>
    public OctreeChunk GetOrLoadChunk(ChunkAddress address)
    {
        TryGetOrLoadChunk(address, out var chunk);
        return chunk!;
    }

    /// <summary>Gets, loads, or materializes a clean zero base-layer chunk.</summary>
    public OctreeChunk GetOrLoadChunk(ChunkCoordinate coordinate) =>
        GetOrLoadChunk(new ChunkAddress(0, coordinate));

    /// <summary>Gets, loads, or materializes a clean zero base-layer chunk.</summary>
    public OctreeChunk GetOrLoadChunk(long chunkX, long chunkY, long chunkZ) =>
        GetOrLoadChunk(new ChunkAddress(0, chunkX, chunkY, chunkZ));

    /// <summary>Loads a stored base-layer chunk, throwing when no file exists.</summary>
    public OctreeChunk LoadChunkFromStorage(ChunkCoordinate coordinate) =>
        LoadChunkFromStorage(new ChunkAddress(0, coordinate));

    /// <summary>Loads a stored base-layer chunk, throwing when no file exists.</summary>
    public OctreeChunk LoadChunkFromStorage(long chunkX, long chunkY, long chunkZ) =>
        LoadChunkFromStorage(new ChunkAddress(0, chunkX, chunkY, chunkZ));

    /// <summary>Loads a stored base or LOD chunk, throwing when no file exists.</summary>
    public OctreeChunk LoadChunkFromStorage(ChunkAddress address)
    {
        if (TryLoadChunkFromStorage(address, out var chunk)) return chunk!;
        throw new FileNotFoundException("The requested chunk is not stored.",
            RequireStorage().GetChunkPath(address));
    }

    /// <summary>Saves dirty least-recently-used chunks and evicts until the soft target is met.</summary>
    public int TrimToMemoryTarget() => TrimToMemoryTargetCore(excluded: null);

    private int TrimToMemoryTargetCore(ChunkAddress? excluded)
    {
        if (!_maxResidentBytes.HasValue) return 0;
        var removed = 0;
        foreach (var candidate in _chunks.GetEvictionCandidates(excluded))
        {
            if (_chunks.EstimatedBytes <= _maxResidentBytes.Value) break;
            if (candidate.IsDirty) SaveChunkCore(candidate.Address, candidate.Value);
            _chunks.Remove(candidate.Address);
            removed++;
        }
        return removed;
    }

    private bool UnloadChunkCore(ChunkAddress address)
    {
        ValidateChunkAddress(address);
        if (!_chunks.TryPeekEntry(address, out var chunk, out var isDirty)) return false;
        if (isDirty && StorageOptions is not null) SaveChunkCore(address, chunk!);
        return _chunks.Remove(address);
    }

    private void SaveChunkCore(ChunkAddress address, OctreeChunk chunk)
    {
        var packet = new byte[chunk.SerializedLength];
        chunk.CopyEncodedTo(packet);
        ChunkFile.Save(RequireStorage(), address, packet);
        _chunks.MarkClean(address, chunk);
    }

    private ChunkStorageOptions RequireStorage() => StorageOptions ??
        throw new InvalidOperationException("Chunk storage is not configured.");

    private int GetMaximumPacketLength() =>
        checked(12 + ChannelCount * (4 + Octree.GetMaximumSize(ChunkShift)));

    private void ValidateStoredSchema(OctreeChunk chunk)
    {
        if (chunk.SideLength != ChunkSize || chunk.ChannelCount != ChannelCount)
            throw new InvalidDataException("Stored chunk schema does not match the world entity.");
    }
}
