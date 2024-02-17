using System.Collections.Concurrent;
using Collections.Isolated.Entities;
using Collections.Isolated.Serialization;
using Collections.Isolated.ValueObjects.Commands;
using Collections.Isolated.ValueObjects.Query;
using Microsoft.Extensions.Logging;

namespace Collections.Isolated;

public sealed class IsolatedDictionary<TValue> where TValue : class 
{
    private readonly ILogger<IsolatedDictionary<TValue>> _logger;

    private readonly ConcurrentDictionary<string, TValue> _dictionary = new();

    private readonly ConcurrentDictionary<string, Transaction<TValue>> _transactions = new();

    private readonly ConcurrentBag<string> _processedTransactions = new();

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public IsolatedDictionary(ILogger<IsolatedDictionary<TValue>> logger)
    {
        _logger = logger;
    }

    internal TValue? Get(string key, string transactionId)
    {
        var transaction = GetOrCreateTransaction(transactionId);

        transaction.AddReadOperation(new QueryKey(key));

        return transaction.Get(key);
    }

    internal void ApplyOperationAsync(WriteOperation<TValue> operation, string transactionId)
    {
        try
        {
            var transaction = GetTransaction(transactionId) ?? CreateTransaction(transactionId);

            transaction.AddWriteOperation(operation);
        }
        catch (Exception)
        {
            _transactions.TryRemove(transactionId, out _);
            throw;
        }
    }

    internal async Task SaveChangesAsync(string transactionId)
    {
        try
        {
            await _semaphore.WaitAsync();

            var transaction = GetTransaction(transactionId);

            if (transaction is null)
            {
                throw new InvalidOperationException("Transaction not found");
            }

            if (transaction.AnyConflictingLockedKeys())
            {
                transaction.RollBackConflictingKeys();

                throw new InvalidOperationException("Transaction has conflicting locked keys");
            }

            transaction.Apply(_dictionary);

            _processedTransactions.Add(transactionId);

            _transactions.Remove(transactionId, out _);

            var transactionsToProcess = transaction.GetOperations();

            transactionsToProcess = transactionsToProcess.ToList().ConvertAll(operation =>
            {
                if (operation is AddOrUpdate<TValue> addOrUpdate)
                {
                    return new AddOrUpdate<TValue>(addOrUpdate.Key, DeepCloneValue(addOrUpdate.Value));
                }

                return operation;
            }).ToHashSet();

            await Parallel.ForEachAsync(_transactions, async (pair, _) => await pair.Value.Sync(transactionsToProcess));
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

    private Transaction<TValue> GetOrCreateTransaction(string transactionId)
    {
        return GetTransaction(transactionId) ?? CreateTransaction(transactionId);
    }

    private Transaction<TValue>? GetTransaction(string transactionId)
    {
        _transactions.TryGetValue(transactionId, out var transaction);

        return transaction;
    }

    internal Transaction<TValue> CreateTransaction(string transactionId)
    {
        var snapshot = new Dictionary<string, TValue>();

        foreach (var (key, value) in _dictionary)
        {
            snapshot.Add(key, DeepCloneValue(value));
        }

        _transactions.TryAdd(transactionId, new Transaction<TValue>(transactionId, _logger, new ConcurrentDictionary<string, TValue>(snapshot)));

        return _transactions[transactionId];
    }

    private TValue DeepCloneValue(TValue value)
    {
        return Serializer.DeserializeFromBytes<TValue>(Serializer.SerializeToBytes(value));
    }

    public int Count()
    {
        return _dictionary.Count;
    }
}