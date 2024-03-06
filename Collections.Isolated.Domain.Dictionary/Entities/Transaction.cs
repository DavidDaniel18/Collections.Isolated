using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Domain.Dictionary.Entities;

internal sealed class Transaction
{
    internal string Id { get; }

    //we compress the log to only show applied values
    private Dictionary<string, WriteOperation> Operations { get; } = new();

    private readonly long _creationTime;

    internal Transaction(string id, long creationTime)
    {
        Id = id;
        _creationTime = creationTime;
    }

    internal void AddOrUpdateOperation(string key, byte[] value)
    {
        var writeOperation = new AddOrUpdate(key, value, Clock.GetTicks());

        AddWriteOperationUnsafe(writeOperation);
    }

    public void AddRemoveOperation(string key)
    {
        var removeOperation = new Remove(key, Clock.GetTicks());

        AddWriteOperationUnsafe(removeOperation);
    }

    internal void Clear()
    {
        Operations.Clear();
    }

    internal byte[]? Get(string key)
    {
        if (Operations.TryGetValue(key, out var operation) && operation is AddOrUpdate addOrUpdate)
        {
            return addOrUpdate.Bytes;
        }

        return null;
    }

    internal IReadOnlyDictionary<string, WriteOperation> GetOperations()
    {
        return Operations;
    }


    internal void Sync(IEnumerable<WriteOperation> operationsToProcess, long commitTime)
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

    private void AddWriteOperationUnsafe(WriteOperation writeOperation)
    {
        Operations[writeOperation.Key] = writeOperation;
    }

    public List<byte[]> GetTrackedEntities()
    {
        return Operations.Values
            .Where(operation => operation is AddOrUpdate)
            .Cast<AddOrUpdate>()
            .Select(operation => operation.Bytes)
            .ToList();
    }
}