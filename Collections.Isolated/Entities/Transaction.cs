using Collections.Isolated.Synchronisation;
using Collections.Isolated.ValueObjects.Commands;

namespace Collections.Isolated.Entities;

internal sealed class Transaction<TValue> where TValue : class
{
    public string Id { get; }

    //we compress the log to only show applied values
    private Dictionary<string, WriteOperation<TValue>> Operations { get; set; } = new();

    internal readonly Dictionary<string, TValue> Snapshot;

    private readonly long _creationTime;

    private readonly IsolationScheduler _scheduler;

    private readonly ReaderWriterLockSlim _operationsSemaphore = new(LockRecursionPolicy.NoRecursion);

    private readonly ReaderWriterLockSlim _snapshotSemaphore = new(LockRecursionPolicy.NoRecursion);

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    internal Transaction(string id, Dictionary<string, TValue> snapshot, long creationTime)
    {
        Id = id;
        Snapshot = snapshot;
        _scheduler = new IsolationScheduler(_cancellationTokenSource);
        _creationTime = creationTime;
    }

    internal void AddOrUpdateOperation(string key, TValue value)
    {
        _scheduler.Schedule(() =>
        {
            try
            {
                var writeOperation = new AddOrUpdate<TValue>(key, value, Clock.GetTicks());

                OperationEnterWriteLock();

                AddWriteOperationUnsafe(writeOperation);
            }
            finally
            {
                _operationsSemaphore.ExitWriteLock();
            }

        }, IsolationScheduler.Priority.High);
    }

    public void AddOrUpdateBatchOperation(IEnumerable<(string key, TValue value)> items)
    {
        _scheduler.Schedule(() =>
        {
            try
            {
                var writeOperations = items.Select(item => new AddOrUpdate<TValue>(item.key, item.value, Clock.GetTicks()));

                OperationEnterWriteLock();

                foreach (var writeOperation in writeOperations)
                {
                    AddWriteOperationUnsafe(writeOperation);
                }
            }
            finally
            {
                _operationsSemaphore.ExitWriteLock();
            }
          
        }, IsolationScheduler.Priority.High);
    }

    public void AddRemoveOperation(string key)
    {
        _scheduler.Schedule(() =>
        {
            try
            {
                var removeOperation = new Remove<TValue>(key, Clock.GetTicks());

                OperationEnterWriteLock();

                AddWriteOperationUnsafe(removeOperation);
            }
            finally
            {
                _operationsSemaphore.ExitWriteLock();
            }
        }, IsolationScheduler.Priority.High);
    }

    internal void Apply()
    {
        _scheduler.Schedule(() =>
        {
            try
            {
                SnapshotEnterWriteLock();

                ApplyChangesUnsafe(Snapshot);
            }
            finally
            {
                _snapshotSemaphore.ExitWriteLock();
            }
        }, IsolationScheduler.Priority.High);
    }

    public void ApplyOneOperation()
    {
        _scheduler.Schedule(() =>
        {
            try
            {
                _operationsSemaphore.EnterUpgradeableReadLock();

                if (Operations.Count == 0) return;

                try
                {
                    _snapshotSemaphore.EnterWriteLock();

                    var operationsToProcess = Operations.Values.First();

                    operationsToProcess.Apply(Snapshot);

                    Operations.Remove(operationsToProcess.Key);
                }
                finally
                {
                    _snapshotSemaphore.ExitWriteLock();
                }
            }
            finally
            {
                _operationsSemaphore.ExitUpgradeableReadLock();

            }

        }, IsolationScheduler.Priority.Medium);
    }

    internal void Clear()
    {
        _scheduler.Schedule(() =>
        {
            try
            {
                OperationEnterWriteLock();

                Operations.Clear();
            }
            finally
            {
                _operationsSemaphore.ExitWriteLock();
            }

        }, IsolationScheduler.Priority.High);
    }

    internal TValue? Get(string key)
    {
        var taskCompletionSource = new TaskCompletionSource<TValue?>();

        _scheduler.Schedule(() =>
        {
            try
            {
                OperationEnterReadLock();

                if (Operations.TryGetValue(key, out var operation) && operation is AddOrUpdate<TValue> addOrUpdate)
                {
                    taskCompletionSource.SetResult(addOrUpdate.LazyValue.Value);

                    return;
                }
            }
            finally
            {
                _operationsSemaphore.ExitReadLock();
            }

            try
            {
                SnapshotEnterReadLock();

                taskCompletionSource.SetResult(Snapshot.GetValueOrDefault(key));
            }
            finally
            {
                _snapshotSemaphore.ExitReadLock();
            }
        }, IsolationScheduler.Priority.High);

        return taskCompletionSource.Task.Result;
    }

    internal Dictionary<string, WriteOperation<TValue>> GetOperations()
    {
        var taskCompletionSource = new TaskCompletionSource<Dictionary<string, WriteOperation<TValue>>>();

        _scheduler.Schedule(() =>
        {
            try
            {
                OperationEnterReadLock();

                taskCompletionSource.SetResult(Operations);
            }
            finally
            {
                _operationsSemaphore.ExitReadLock();
            }
        }, IsolationScheduler.Priority.High);

        return taskCompletionSource.Task.Result;
    }

    internal void LazySync(Dictionary<string, WriteOperation<TValue>> operationsToProcess, long commitTime)
    {
        _scheduler.Schedule(() =>
        {
            try
            {
                _operationsSemaphore.EnterWriteLock();

                var newOperations = operationsToProcess.Where(op =>
                {

                    if (Operations.TryGetValue(op.Key, out var persistedOperation))
                    {
                        var persistedTicks = persistedOperation.CreationTime;

                        var operationTicks = op.Value.CreationTime;

                        // we only want to apply operations that are newer than the persisted ones
                        return operationTicks > persistedTicks;
                    }

                    // we only want to apply operations that are newer than the transaction
                    return commitTime > _creationTime;
                }).ToList();

                foreach (var operation in newOperations)
                {
                    AddWriteOperationUnsafe(operation.Value.LazyDeepCloneValue());
                }
            }
            finally
            {
                _operationsSemaphore.ExitWriteLock();
            }
        }, IsolationScheduler.Priority.High);
        
    }

    private void AddWriteOperationUnsafe(WriteOperation<TValue> writeOperation)
    {
        Operations[writeOperation.Key] = writeOperation;
    }

    private void ApplyChangesUnsafe(Dictionary<string, TValue> store)
    {
        foreach (var operation in Operations.Values)
        {
            operation.Apply(store);
        }
    }

    public void StopProcessing()
    {
        _scheduler.WaitForCompletionAsync();
    }

    private void OperationEnterReadLock()
    {
        if (_operationsSemaphore.TryEnterReadLock(100) is false) throw new TimeoutException("Could not acquire read lock");
    }

    private void OperationEnterWriteLock()
    {
        if (_operationsSemaphore.TryEnterWriteLock(100) is false) throw new TimeoutException("Could not acquire read lock");
    }

    private void SnapshotEnterReadLock()
    {
        if (_snapshotSemaphore.TryEnterReadLock(100) is false) throw new TimeoutException("Could not acquire read lock");
    }

    private void SnapshotEnterWriteLock()
    {
        if (_snapshotSemaphore.TryEnterWriteLock(100) is false) throw new TimeoutException("Could not acquire read lock");
    }
}