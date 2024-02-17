using System.Collections.Concurrent;
using System.Collections.Immutable;
using Collections.Isolated.ValueObjects.Commands;
using Collections.Isolated.ValueObjects.Query;
using Microsoft.Extensions.Logging;

namespace Collections.Isolated.Entities;

internal sealed class Transaction<TValue> where TValue : class
{
    private readonly ILogger _logger;

    private string Id { get; }

    private readonly SemaphoreSlim _operationsSemaphore = new(1, 1);

    //we compress the log to only show applied values
    private ConcurrentDictionary<string, WriteOperation<TValue>> Operations { get; set; } = new();

    private readonly SemaphoreSlim _keySemaphore = new(1, 1);

    private ImmutableHashSet<string> _lockedKeys = ImmutableHashSet<string>.Empty;

    private readonly ConcurrentDictionary<string, TValue> _snapshot;

    public Transaction(string id, ILogger logger, ConcurrentDictionary<string, TValue> snapshot)
    {
        _logger = logger;
        Id = id;
        _snapshot = snapshot;
    }

    public void AddReadOperation(ReadOperation readOperation)
    {
        UnlockKeysOnRead(readOperation);
    }

    public void AddWriteOperation(WriteOperation<TValue> writeOperation)
    {
        try
        {
            _operationsSemaphore.Wait();

            if (Operations.ContainsKey(writeOperation.Key))
            {
                Operations.TryRemove(writeOperation.Key, out _);
            }

            Operations[writeOperation.Key] = writeOperation;
        }
        finally
        {
            _operationsSemaphore.Release();
        }
    }

    private void UnlockKeysOnRead(ReadOperation readOperation)
    {
        try
        {
            _keySemaphore.Wait();

            _lockedKeys = readOperation switch
            {
                QueryKey queryKey when _lockedKeys.Contains(queryKey.Key) => _lockedKeys.Remove(queryKey.Key),
                QueryAll => _lockedKeys.Clear(),
                _ => _lockedKeys
            };
        }
        finally
        {
            _keySemaphore.Release();
        }
    }

    internal void Apply(ConcurrentDictionary<string, TValue> store)
    {
        if (Operations.Any() is false) return;

        try
        {
            _operationsSemaphore.Wait();

            foreach (var operation in Operations.Values)
            {
                operation.Apply(store);
            }
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

    public void Clear()
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

    public TValue? Get(string key)
    {
        if (Operations.TryGetValue(key, out var operation) && operation is AddOrUpdate<TValue> addOrUpdate)
        {
            return addOrUpdate.Value;
        }

        return _snapshot.GetValueOrDefault(key);
    }

    public HashSet<WriteOperation<TValue>> GetOperations()
    {
        return [..Operations.Values];
    }

    public async Task Sync(HashSet<WriteOperation<TValue>> operationsToProcess)
    {
        try
        {
            await _keySemaphore.WaitAsync();

            foreach (var operation in operationsToProcess)
            {
                _lockedKeys = _lockedKeys.Add(operation.Key);

                AddWriteOperation(operation);
            }

            Apply(_snapshot);
        }
        finally
        {
            _keySemaphore.Release();
        }
    }

    public bool AnyConflictingLockedKeys()
    {
        var intersected = _lockedKeys.Intersect(Operations.Select(op => op.Key)).Count > 0;

        return intersected;
    }

    public void RollBackConflictingKeys()
    {
        try
        {
            _operationsSemaphore.Wait();

            foreach (var key in _lockedKeys)
            {
                Operations.TryRemove(key, out _);
            }
        }
        finally
        {
            _operationsSemaphore.Release();
        }
    }
}