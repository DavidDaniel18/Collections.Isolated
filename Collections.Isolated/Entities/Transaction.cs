using System.Collections.Concurrent;
using System.Collections.Immutable;
using Collections.Isolated.Monads;
using Collections.Isolated.ValueObjects;
using Collections.Isolated.ValueObjects.Commands;
using Collections.Isolated.ValueObjects.Query;

namespace Collections.Isolated.Entities;

internal sealed class Transaction<TKey, TValue>
    where TValue : class
    where TKey : notnull
{
    public string Id { get; }

    public DateTime CreatedAt { get; private set; } = DateTime.UnixEpoch;

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    internal ImmutableList<Operation<TKey, TValue>> Operations { get; private set; } = ImmutableList<Operation<TKey, TValue> >.Empty;

    private ImmutableHashSet<TKey> _lockedKeys = ImmutableHashSet<TKey>.Empty;

    private bool _isBlocked;

    public Transaction(string id)
    {
        Id = id;
    }

    public void Block()
    {
        _isBlocked = true;
    }

    public async Task<Result> Add(Operation<TKey, TValue> operation)
    {
        if (_isBlocked) return Result.Failure($"Transaction {Id} is blocked, cannot add {operation.GetType()} on {typeof(TValue)}");

        if (Operations.Exists(op => op.Id.Equals(operation.Id))) return Result.Success();

        try
        {
            await _semaphore.WaitAsync();

            if (Operations.IsEmpty)
            {
                CreatedAt = DateTime.UtcNow;
            }

            switch (operation)
            {
                //any read operation will unlock the keys
                case ReadOperation<TKey, TValue> readOperation:
                    _lockedKeys = readOperation switch
                    {
                        QueryKey<TKey, TValue> queryKey when _lockedKeys.Contains(queryKey.Key) => _lockedKeys.Remove(queryKey.Key),
                        QueryAll<TKey, TValue> => _lockedKeys.Clear(),
                        _ => _lockedKeys
                    };
                    break;
                case WriteOperation<TKey, TValue> writeOperation when writeOperation.IsKeyColliding(_lockedKeys):
                    return Result.Failure("Transaction is colliding with another transaction");
            }

            Operations = Operations.Add(operation).OrderBy(op => op.CreatedAt).ToImmutableList();

            return Result.Success();
        }
        catch
        {
            return Result.Failure("Error while applying operation");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Result> Apply(ConcurrentDictionary<TKey, TValue> store)
    {
        if(_isBlocked) return Result.Failure($"Transaction {Id} is blocked, Cannot Save Changes on {typeof(TValue)}");

        try
        {
            await _semaphore.WaitAsync();

            var result = Result.Foreach(Operations, operation =>
            {
                if (operation is WriteOperation<TKey, TValue> writeOperation)
                {
                    return writeOperation.Apply(store);
                }

                return Result.Success();
            });

            return result;
        }
        catch
        {
            return Result.Failure("Error while applying operation");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Result<ConcurrentDictionary<TKey,TValue>> VirtualApply(ConcurrentDictionary<TKey, TValue> store)
    {
        var copy = new ConcurrentDictionary<TKey, TValue>(store);

        return Result.Foreach(Operations, operation =>
        {
            if (operation is WriteOperation<TKey, TValue> writeOperation)
            {
                return writeOperation.Apply(copy);
            }

            return Result.Success();
        }).Bind(() => Result.Success(copy));
    }

    public void Clear()
    {
        Operations = ImmutableList<Operation<TKey, TValue>>.Empty;
    }

    public async Task<bool> CollidesWithNewState(Transaction<TKey, TValue> newStateTransaction)
    {
        if (_isBlocked) return false;

        try
        {
            await _semaphore.WaitAsync();

            var newWrites = newStateTransaction.GetWriteOperations();

            var datetimeByWrittenKeys =
                from write in newWrites
                let keysAndTimes = (write.GetKeys(), write.CreatedAt)
                from key in keysAndTimes.Item1
                let keyAndTime = (key, keysAndTimes.CreatedAt)
                group keyAndTime by keyAndTime.key into grouped
                select grouped.MinBy(g => g.CreatedAt);

            var newDatetimeByWrittenKeysDict = datetimeByWrittenKeys.ToDictionary(tuple => tuple.key, tuple => tuple.CreatedAt);

            if (Operations.Any(operation => operation is WriteOperation<TKey, TValue>) is false)
            {
                foreach (var operation in Operations)
                {
                    _lockedKeys = operation switch
                    {
                        QueryKey<TKey, TValue> queryKey => newDatetimeByWrittenKeysDict.ContainsKey(queryKey.Key) ? _lockedKeys.Add(queryKey.Key) : _lockedKeys,
                        QueryAll<TKey, TValue> => _lockedKeys.Union(newDatetimeByWrittenKeysDict.Keys.ToImmutableHashSet()),
                        _ => _lockedKeys
                    };
                }
            }

            foreach (var operation in Operations)
            {
                if (operation is WriteOperation<TKey, TValue> writeOperation)
                {
                    if (IsAnyOperationSharingKeysAndHappeningBeforeNewWrite(writeOperation, newDatetimeByWrittenKeysDict)) 
                        return true;
                }
            }

            _lockedKeys = _lockedKeys.Union(newWrites.SelectMany(write => write.GetKeys()));

            return false;
        }
        finally
        {
            _semaphore.Release();
        }
       
    }

    private static bool IsAnyOperationSharingKeysAndHappeningBeforeNewWrite(WriteOperation<TKey, TValue> writeOperation, Dictionary<TKey, DateTime> newDatetimeByWrittenKeysDict)
    {
        return writeOperation.GetKeys().Any(key =>
        {
            if (newDatetimeByWrittenKeysDict.TryGetValue(key, out var newDatetime))
            {
                return newDatetime > writeOperation.CreatedAt;
            }

            return false;
        });
    }

    public List<WriteOperation<TKey,TValue>> GetWriteOperations()
    {
        return Operations.Where(operation => operation is WriteOperation<TKey, TValue>).Cast<WriteOperation<TKey, TValue>>().ToList();
    }

    public bool HasAnyWrites()
    {
        return GetWriteOperations().Any();
    }
}