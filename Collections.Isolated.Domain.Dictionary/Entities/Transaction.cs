using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Domain.Dictionary.Entities;

internal sealed class Transaction<TValue> where TValue : class
{
    internal string Id { get; }

    //we compress the log to only show applied values
    private Dictionary<string, WriteOperation<TValue>> Operations { get; } = new();

    private readonly Dictionary<string, TValue> _snapshot;

    private readonly long _creationTime;

    internal Transaction(string id, Dictionary<string, TValue> snapshot, long creationTime)
    {
        Id = id;
        _snapshot = snapshot;
        _creationTime = creationTime;
    }

    internal void AddOrUpdateOperation(string key, TValue value)
    {
        var writeOperation = new AddOrUpdate<TValue>(key, value, Clock.GetTicks());

        AddWriteOperationUnsafe(writeOperation);
    }

    public void AddRemoveOperation(string key)
    {
        var removeOperation = new Remove<TValue>(key, Clock.GetTicks());

        AddWriteOperationUnsafe(removeOperation);
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

        return _snapshot.GetValueOrDefault(key);
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
            AddWriteOperationUnsafe(operation);
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

    public IEnumerable<TValue> GetTrackedEntities()
    {
        return Operations.Values
            .Where(operation => operation is AddOrUpdate<TValue>)
            .Cast<AddOrUpdate<TValue>>()
            .Select(operation => operation.LazyValue)
            .ToList();
    }

    public IEnumerable<TValue> GetAll()
    {
        return _snapshot.Values.ToArray();
    }
}