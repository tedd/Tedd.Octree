using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Tedd.Voxtree;

public sealed partial class OctreeWorld : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    /// <summary>Holds a shared lock across multiple calls on this world.</summary>
    /// <remarks>
    /// Use a synchronous using scope. Other readers may run concurrently; writers wait.
    /// Writes inside a read batch throw <see cref="LockRecursionException"/>.
    /// </remarks>
    public Batch BeginReadBatch()
    {
        _lock.EnterReadLock();
        return new Batch(_lock, write: false);
    }

    /// <summary>Holds an exclusive lock across multiple reads and writes on this world.</summary>
    /// <remarks>
    /// Changes become visible to other threads when the outermost batch ends. This is
    /// mutual exclusion, not rollback: failures leave preceding successful operations applied.
    /// Build chunk snapshots before entering the batch to minimize time blocking readers.
    /// </remarks>
    /// <exception cref="LockRecursionException">The thread holds only a read batch.</exception>
    public Batch BeginWriteBatch()
    {
        _lock.EnterWriteLock();
        return new Batch(_lock, write: true);
    }

    /// <summary>A stack-only, thread-affine world lock scope.</summary>
    /// <remarks>
    /// Dispose on the creating thread in reverse acquisition order. Do not copy scopes
    /// or hold them across await. Compatible batches may nest; read-to-write upgrades
    /// are not supported. A default or already-disposed scope does nothing on disposal.
    /// </remarks>
    public ref struct Batch
    {
        private ReaderWriterLockSlim? _owner;
        private readonly bool _write;
        private readonly int _threadId, _readDepth, _writeDepth;

        internal Batch(ReaderWriterLockSlim owner, bool write)
        {
            _owner = owner;
            _write = write;
            _threadId = Environment.CurrentManagedThreadId;
            _readDepth = owner.RecursiveReadCount;
            _writeDepth = owner.RecursiveWriteCount;
        }

        /// <summary>Releases this batch's lock on the creating thread.</summary>
        public void Dispose()
        {
            var owner = _owner;
            if (owner is null) return;
            if (Environment.CurrentManagedThreadId != _threadId ||
                owner.RecursiveReadCount != _readDepth || owner.RecursiveWriteCount != _writeDepth)
                throw new SynchronizationLockException("Dispose batches on their creating thread in reverse acquisition order; do not copy them.");
            if (_write) owner.ExitWriteLock();
            else owner.ExitReadLock();
            _owner = null;
        }
    }

    // Ordinary calls borrow an enclosing batch's lock, avoiding per-operation re-entry.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private OperationLock ReadLock()
    {
        if (_lock.IsReadLockHeld || _lock.IsWriteLockHeld) return default;
        _lock.EnterReadLock();
        return new OperationLock(_lock, write: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private OperationLock WriteLock()
    {
        if (_lock.IsWriteLockHeld) return default;
        _lock.EnterWriteLock(); // Rejects attempts to upgrade an ordinary read batch.
        return new OperationLock(_lock, write: true);
    }

    private readonly struct OperationLock : IDisposable
    {
        private readonly ReaderWriterLockSlim? _owner;
        private readonly bool _write;

        internal OperationLock(ReaderWriterLockSlim owner, bool write)
        {
            _owner = owner;
            _write = write;
        }

        public void Dispose()
        {
            if (_owner is null) return;
            if (_write) _owner.ExitWriteLock();
            else _owner.ExitReadLock();
        }
    }

    /// <summary>Releases synchronization resources after all users of this world have stopped.</summary>
    /// <remarks>Do not dispose concurrently with operations or while any batch is active.</remarks>
    public void Dispose() => _lock.Dispose();
}
