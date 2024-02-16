using System.Collections.Concurrent;
using System.Text.Json;
using Collections.Isolated.Entities;
using Collections.Isolated.Monads;
using Collections.Isolated.ValueObjects;
using Collections.Isolated.ValueObjects.Commands;
using Collections.Isolated.ValueObjects.Query;

namespace Collections.Isolated;

public sealed class IsolatedDictionary<TKey, TValue> 
    where TValue : class 
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _dictionary = new();

    private readonly ConcurrentDictionary<string, Transaction<TKey, TValue>> _transactions = new();

    private readonly ConcurrentBag<string> _processedTransactions = new();

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    internal async Task<Result<TValue>> Get(TKey key, string transactionId)
    {
        var transaction = GetTransaction(transactionId);

        return await transaction.VirtualApply(_dictionary)
            .BindAsync(async virtualCopy =>
            {
                if (virtualCopy.TryGetValue(key, out var value))
                {
                    await transaction.Add(new QueryKey<TKey, TValue>(key));

                    return DeepCloneValue(value);
                }

                return Result.Failure<TValue>($"Key {key} not found in cache");
            });
    }

    internal async Task<Result<List<TValue>>> Query(Func<IEnumerable<TValue>, IEnumerable<TValue>> filter, string transactionId)
    {
        var transaction = GetTransaction(transactionId);

        return await transaction.VirtualApply(_dictionary)
            .BindAsync(async virtualCopy =>
            {
                var filteredItems = filter(virtualCopy.Values)
                    .Select(value => DeepCloneValue(value).GetValueOrThrow())
                    .ToList();

                await transaction.Add(new QueryAll<TKey, TValue>());

                return Result.Success(filteredItems);
            });
    }

    internal async Task<Result> ApplyAsync(Operation<TKey, TValue> operation, string transactionId)
    {
        return 
            await GetTransaction(transactionId)
            .Add(operation)
            .FailedAsync(exception => _transactions.TryRemove(transactionId, out _));
    }

    internal async Task<Result<List<WriteOperation<TKey, TValue>>>> SaveChangesAsync(string transactionId)
    {
        await _semaphore.WaitAsync();

        try
        {
            var transaction = GetTransaction(transactionId);

            var transactionsIdToBlock = new ConcurrentBag<string>();

            var result = await Result.ForeachAsync(_transactions, async pair =>
                {
                    if (pair.Key == transactionId) return Result.Success();

                    if (await pair.Value.CollidesWithNewState(transaction))
                    {
                        transactionsIdToBlock.Add(pair.Key);
                    }

                    return Result.Success();
                })
                .BindAsync(() => transactionsIdToBlock.ToList().ForEach(id => _transactions[id].Block()))
                .BindAsync(() => transaction.Apply(_dictionary));

            result.ThrowIfException();

            _processedTransactions.Add(transactionId);

            _transactions.Remove(transactionId, out _);

            return Result.Success(transaction.Operations.Where(operation => operation is WriteOperation<TKey, TValue>)
                .Cast<WriteOperation<TKey, TValue>>().ToList());
        }
        catch(Exception e)
        {
            return Result.Failure<List<WriteOperation<TKey, TValue>>>($"Error while saving changes: {e}");
        }
        finally
        {
            _semaphore.Release();
        }

    }

    internal void UndoChanges(string transactionId)
    {
        if (_transactions.TryRemove(transactionId, out var transaction))
        {
            transaction.Clear();
        }
    }

    private Transaction<TKey, TValue> GetTransaction(string transactionId)
    {
        return _transactions.GetOrAdd(transactionId, new Transaction<TKey, TValue>(Guid.NewGuid().ToString()));
    }

    private Result<TValue> DeepCloneValue(TValue value)
    {
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

        string json = JsonSerializer.Serialize(value, options);

        var outValue = JsonSerializer.Deserialize<TValue>(json, options);

        return outValue is not null
            ? Result.Success(outValue)
            : Result.Failure<TValue>("Error while cloning value");
    }
}