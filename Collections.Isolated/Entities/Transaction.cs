using System.Collections.Concurrent;
using System.Collections.Immutable;
using Collections.Isolated.ValueObjects.Commands;
using Microsoft.Extensions.Logging;

namespace Collections.Isolated.Entities;

internal sealed class Transaction<TValue> where TValue : class
{
    private readonly ILogger _logger;

    private string Id { get; }

    private readonly SemaphoreSlim _operationsSemaphore = new(1, 1);

    //we compress the log to only show applied values
    private Dictionary<string, WriteOperation<TValue>> Operations { get; set; } = new();

    private readonly ConcurrentDictionary<string, TValue> _snapshot;

    public readonly DateTime CreationTime;

    internal Transaction(string id, ILogger logger, ConcurrentDictionary<string, TValue> snapshot, DateTime creationTime)
    {
        _logger = logger;
        Id = id;
        _snapshot = snapshot;
        CreationTime = creationTime;
    }

    internal void AddOrUpdateOperation(string key, TValue value)
    {
        try
        {
            var writeOperation = new AddOrUpdate<TValue>(key, value, DateTime.UtcNow);

            _operationsSemaphore.Wait();

            AddWriteOperationUnsafe(writeOperation);
        }
        finally
        {
            _operationsSemaphore.Release();
        }
    }

    public void AddOrUpdateBatchOperation(IEnumerable<(string key, TValue value)> items)
    {
        foreach (var (key, value) in items)
        {
            AddOrUpdateOperation(key, value);
        }
    }

    public void AddRemoveOperation(string key)
    {
        try
        {
            var writeOperation = new Remove<TValue>(key, DateTime.UtcNow);

            _operationsSemaphore.Wait();

            AddWriteOperationUnsafe(writeOperation);
        }
        finally
        {
            _operationsSemaphore.Release();
        }
    }

    internal void Apply(ConcurrentDictionary<string, TValue> store)
    {
        if (Operations.Any() is false) return;

        try
        {
            _operationsSemaphore.Wait();

            ApplyChangesUnsafe(store);
        }
        catch
        {
            _logger.LogError("Error applying transaction {Id}", Id);
            throw;
        }
        finally
        {
            _operationsSemaphore.Release();
        }
    }

    internal void Clear()
    {
        try
        {
            _operationsSemaphore.Wait();

            Operations.Clear();
            _snapshot.Clear();
        }
        finally
        {
            _operationsSemaphore.Release();
        }
    }

    internal TValue? Get(string key)
    {
        if (Operations.TryGetValue(key, out var operation) && operation is AddOrUpdate<TValue> addOrUpdate)
        {
            return addOrUpdate.LazyValue.Value;
        }

        return _snapshot.GetValueOrDefault(key);
    }

    internal List<WriteOperation<TValue>> GetOperations()
    {
        try
        {
            _operationsSemaphore.Wait();

            return [.. Operations.Values];
        }
        finally
        {
            _operationsSemaphore.Release();
        }
    }

    internal void LazySync(IEnumerable<WriteOperation<TValue>> operationsToProcess)
    {
        foreach (var operation in operationsToProcess.Where(operation => CreationTime < operation.DateTime))
        {
            AddWriteOperationUnsafe(operation.LazyDeepCloneValue(CreationTime));
        }
    }

    private void AddWriteOperationUnsafe(WriteOperation<TValue> writeOperation)
    {
        if(writeOperation is Remove<TValue> removeOperation)
            removeOperation.Apply(_snapshot);

        Operations.Remove(writeOperation.Key);

        Operations[writeOperation.Key] = writeOperation;
    }

    private void ApplyChangesUnsafe(ConcurrentDictionary<string, TValue> store)
    {
        foreach (var operation in Operations.Values)
        {
            operation.Apply(store);
        }
    }
}