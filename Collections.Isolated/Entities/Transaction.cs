using Collections.Isolated.Synchronisation;
using Collections.Isolated.ValueObjects.Commands;

namespace Collections.Isolated.Entities;

internal sealed class Transaction<TValue> where TValue : class
{
    public string Id { get; }

    //we compress the log to only show applied values
    private Dictionary<string, WriteOperation<TValue>> Operations { get; set; } = new();

    internal readonly Dictionary<string, TValue> Snapshot;

    private readonly DateTime _creationTime;

    internal readonly IsolationScheduler _scheduler;

    private readonly ReaderWriterLockSlim _operationsSemaphore = new(LockRecursionPolicy.NoRecursion);

    private readonly ReaderWriterLockSlim _snapshotSemaphore = new(LockRecursionPolicy.NoRecursion);

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    internal Transaction(string id, Dictionary<string, TValue> snapshot, DateTime creationTime)
    {
        Id = id;
        Snapshot = snapshot;
        _scheduler = new IsolationScheduler(_cancellationTokenSource);
        _creationTime = creationTime;
    }

    internal void AddOrUpdateOperation(string key, TValue value)
    {
        var writeOperation = new AddOrUpdate<TValue>(key, value, DateTime.UtcNow);

        _scheduler.Schedule(() =>
        {
            try
            {
                _operationsSemaphore.EnterWriteLock();

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
        var writeOperations = items.Select(item => new AddOrUpdate<TValue>(item.key, item.value, DateTime.UtcNow));

        _scheduler.Schedule(() =>
        {
            try
            {
                _operationsSemaphore.EnterWriteLock();

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
        var removeOperation = new Remove<TValue>(key, DateTime.UtcNow);

        _scheduler.Schedule(() =>
        {
            try
            {
                _operationsSemaphore.EnterWriteLock();

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
        if (Operations.Count == 0) return;

        _scheduler.Schedule(() =>
        {
            try
            {
                _snapshotSemaphore.EnterWriteLock();

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
        _scheduler.Schedule(() => Operations.Clear(), IsolationScheduler.Priority.High);
    }

    internal TValue? Get(string key)
    {
        var taskCompletionSource = new TaskCompletionSource<TValue?>();

        _scheduler.Schedule(() =>
        {
            try
            {
                _operationsSemaphore.EnterReadLock();

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
                _snapshotSemaphore.EnterReadLock();

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
                _operationsSemaphore.EnterReadLock();

                taskCompletionSource.SetResult(Operations);
            }
            finally
            {
                _operationsSemaphore.ExitReadLock();
            }
        }, IsolationScheduler.Priority.High);

        return taskCompletionSource.Task.Result;
    }

    internal void LazySync(Dictionary<string, WriteOperation<TValue>> operationsToProcess, DateTime commitTime)
    {
        var newOperations = operationsToProcess.Where(op =>
        {

            if (Operations.TryGetValue(op.Key, out var persistedOperation))
            {
                var persistedTicks = persistedOperation.DateTime.Ticks;

                var operationTicks = op.Value.DateTime.Ticks;

                // we only want to apply operations that are newer than the persisted ones
                return operationTicks > persistedTicks;
            }

            var transactionTicks = _creationTime.Ticks;

            var commitTicks = commitTime.Ticks;
            
            // we only want to apply operations that are newer than the transaction
            return commitTicks > transactionTicks;
        }).ToList();

        _scheduler.Schedule(() =>
        {
            try
            {
                _operationsSemaphore.EnterWriteLock();

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
        _scheduler.BlockAdditions();
        _scheduler.WaitForCompletionAsync();
        _scheduler.StopProcessing();
        _cancellationTokenSource.Cancel();
    }
}