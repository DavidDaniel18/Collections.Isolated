using System.Collections.Immutable;
using Collections.Isolated.Synchronisation;
using Collections.Isolated.ValueObjects.Commands;

namespace Collections.Isolated.Entities;

internal sealed class Transaction<TValue> where TValue : class
{
    public string Id { get; }

    //we compress the log to only show applied values
    private Dictionary<string, WriteOperation<TValue>> Operations { get; } = new();

    internal readonly Dictionary<string, TValue> Snapshot;

    private readonly long _creationTime;

    private readonly IsolationScheduler _scheduler;

    internal Transaction(string id, Dictionary<string, TValue> snapshot, long creationTime)
    {
        Id = id;
        Snapshot = snapshot;
        _scheduler = new IsolationScheduler();
        _creationTime = creationTime;
    }

    internal void AddOrUpdateOperation(string key, TValue value)
    {
        _scheduler.Schedule(() =>
        {
            var writeOperation = new AddOrUpdate<TValue>(key, value, Clock.GetTicks());

            AddWriteOperationUnsafe(writeOperation);

        }, IsolationScheduler.Priority.High);
    }

    public void AddOrUpdateBatchOperation(IEnumerable<(string key, TValue value)> items)
    {
        _scheduler.Schedule(() =>
        {
            var writeOperations = items.Select(item => new AddOrUpdate<TValue>(item.key, item.value, Clock.GetTicks()));

            foreach (var writeOperation in writeOperations)
            {
                AddWriteOperationUnsafe(writeOperation);
            }

        }, IsolationScheduler.Priority.High);
    }

    public void AddRemoveOperation(string key)
    {
        _scheduler.Schedule(() =>
        {
            var removeOperation = new Remove<TValue>(key, Clock.GetTicks());

            AddWriteOperationUnsafe(removeOperation);
        }, IsolationScheduler.Priority.High);
    }

    internal void Apply()
    {
        _scheduler.Schedule(() => ApplyChangesUnsafe(Snapshot), IsolationScheduler.Priority.High);
    }

    public void ApplyOneOperation()
    {
        _scheduler.Schedule(() =>
        {
            if (Operations.Count == 0) return;

            var operationsToProcess = Operations.Values.First();

            operationsToProcess.Apply(Snapshot);

            Operations.Remove(operationsToProcess.Key);

        }, IsolationScheduler.Priority.Medium);
    }

    internal void Clear()
    {
        _scheduler.Schedule(() => Operations.Clear(), IsolationScheduler.Priority.High);
    }

    internal async Task<TValue?> GetAsync(string key)
    {
        var taskCompletionSource = new TaskCompletionSource<TValue?>();

        _scheduler.Schedule(() =>
        {
            if (Operations.TryGetValue(key, out var operation) && operation is AddOrUpdate<TValue> addOrUpdate)
            {
                taskCompletionSource.SetResult(addOrUpdate.LazyValue.Value);

                return;
            }

            Interlocked.MemoryBarrier();

            taskCompletionSource.SetResult(Snapshot.GetValueOrDefault(key));

        }, IsolationScheduler.Priority.High);

        return await taskCompletionSource.Task.ConfigureAwait(false);
    }

    internal async Task<Dictionary<string, WriteOperation<TValue>>> GetOperationsAsync()
    {
        var taskCompletionSource = new TaskCompletionSource<Dictionary<string, WriteOperation<TValue>>>();

        _scheduler.Schedule(() => taskCompletionSource.SetResult(Operations), IsolationScheduler.Priority.High);

        return await taskCompletionSource.Task.ConfigureAwait(false);
    }


    internal void Sync(IEnumerable<WriteOperation<TValue>> operationsToProcess, long commitTime)
    {
        _scheduler.Schedule(() => SyncNewerLogs(operationsToProcess, commitTime), IsolationScheduler.Priority.High);
    }

    internal void LazySync(IEnumerable<WriteOperation<TValue>> operationsToProcess, long commitTime)
    {
        _scheduler.Schedule(() =>
        {
            SyncNewerLogs(operationsToProcess, commitTime);

            foreach (var operations in Operations.Values)
            {
                operations.Apply(Snapshot);
            }
        }, IsolationScheduler.Priority.Medium);
    }

    private void SyncNewerLogs(IEnumerable<WriteOperation<TValue>> operationsToProcess, long commitTime)
    {
        var newOperations = operationsToProcess.Where(op =>
        {
            if (Operations.TryGetValue(op.Key, out var persistedOperation))
            {
                var persistedTicks = persistedOperation.CreationTime;

                var operationTicks = op.CreationTime;

                // we only want to apply operations that are newer than the persisted ones
                return operationTicks > persistedTicks;
            }

            // we only want to apply operations that are newer than the transaction
            return commitTime > _creationTime;
        });

        foreach (var operation in newOperations)
        {
            AddWriteOperationUnsafe(operation.LazyDeepCloneValue());
        }
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

    public async Task StopProcessing()
    {
        await _scheduler.WaitForCompletionAsync();
    }
}