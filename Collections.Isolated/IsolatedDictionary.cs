using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Transactions;
using Collections.Isolated.Entities;
using Collections.Isolated.ValueObjects.Commands;

namespace Collections.Isolated;

internal sealed class IsolatedDictionary<TValue> where TValue : class
{
    private ConcurrentDictionary<string, TValue> _dictionary = new();

    private readonly ConcurrentDictionary<string, Transaction<TValue>> _transactions = new();

    internal async Task<Dictionary<string, WriteOperation<TValue>>> SaveChangesAsync(string transactionId)
    {
        var transaction = _transactions[transactionId];

        _transactions.Remove(transaction.Id, out _);

        transaction.Apply();

        var operations = await transaction.GetOperationsAsync();

        await transaction.StopProcessing();

        _dictionary = new ConcurrentDictionary<string, TValue>(transaction.Snapshot);

        return operations;
    }

    internal int Count()
    {
        return _dictionary.Count;
    }

    public async Task<TValue?> GetAsync(string key, string transactionId)
    {
        var transaction = _transactions[transactionId];

        return await transaction.GetAsync(key);
    }

    public void AddOrUpdate(string key, TValue value, string transactionId)
    {
        var transaction = _transactions[transactionId];

        transaction.AddOrUpdateOperation(key, value);
    }

    public void Remove(string key, string transactionId)
    {
        var transaction = _transactions[transactionId];

        transaction.AddRemoveOperation(key);
    }

    public void BatchAddOrUpdate(IEnumerable<(string key, TValue value)> items, string transactionId)
    {
        var transaction = _transactions[transactionId];

        transaction.AddOrUpdateBatchOperation(items);
    }

    internal void UndoChanges(string transactionId)
    {
        if (_transactions.TryRemove(transactionId, out var transaction))
        {
            transaction.Clear();
        }
    }

    internal void EnsureTransactionCreated(string transactionId)
    {
        if(ContainsTransaction(transactionId) is false)
        {
            CreateTransaction(transactionId);
        }
    }

    internal bool ContainsTransaction(string transactionId)
    {
        return _transactions.ContainsKey(transactionId);
    }

    internal void UpdateTransactionWithLog(string transactionId, IEnumerable<WriteOperation<TValue>> log, long lastLogTime)
    {
        var transaction = _transactions[transactionId];

        transaction.Sync(log, lastLogTime);
    }

    private void CreateTransaction(string transactionId)
    {
        var snapshot = new Dictionary<string, TValue>();

        foreach (var (key, value) in _dictionary)
        {
            snapshot.Add(key, value);
        }

        var transaction = new Transaction<TValue>(transactionId, new Dictionary<string, TValue>(snapshot), Clock.GetTicks());

        _transactions.TryAdd(transactionId, transaction);
    }

    public void LazyUpdateTransactions(string exceptTransactionId, List<WriteOperation<TValue>> log, long lastLogTime)
    {
        Parallel.ForEach(_transactions.Values, (transaction, _) =>
        {
            if (transaction.Id != exceptTransactionId)
            {
                transaction.LazySync(log, lastLogTime);
            }
        });
    }
}