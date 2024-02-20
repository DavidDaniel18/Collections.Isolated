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

    internal Transaction(string id, Dictionary<string, TValue> snapshot, long creationTime)
    {
        Id = id;
        Snapshot = snapshot;
        _creationTime = creationTime;
    }

    internal void AddOrUpdateOperation(string key, TValue value)
    {
        var writeOperation = new AddOrUpdate<TValue>(key, value, Clock.GetTicks());

        AddWriteOperationUnsafe(writeOperation);
    }

    public void AddOrUpdateBatchOperation(IEnumerable<(string key, TValue value)> items)
    {
        var writeOperations = items.Select(item => new AddOrUpdate<TValue>(item.key, item.value, Clock.GetTicks()));

        foreach (var writeOperation in writeOperations)
        {
            AddWriteOperationUnsafe(writeOperation);
        }
    }

    public void AddRemoveOperation(string key)
    {
        var removeOperation = new Remove<TValue>(key, Clock.GetTicks());

        AddWriteOperationUnsafe(removeOperation);
    }

    internal void Apply()
    {
        ApplyChangesUnsafe(Snapshot);
    }

    internal void Clear()
    {
        Operations.Clear();
    }

    internal TValue? Get(string key)
    {
        if (Operations.TryGetValue(key, out var operation) && operation is AddOrUpdate<TValue> addOrUpdate)
        {
            return addOrUpdate.LazyValue;
        }

        return Snapshot.GetValueOrDefault(key);
    }

    internal IReadOnlyDictionary<string, WriteOperation<TValue>> GetOperations()
    {
        return Operations;
    }


    internal void Sync(IEnumerable<WriteOperation<TValue>> operationsToProcess, long commitTime)
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
}