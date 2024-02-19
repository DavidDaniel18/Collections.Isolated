using System.Collections.Concurrent;
using Collections.Isolated.Entities;
using Collections.Isolated.ValueObjects.Commands;

namespace Collections.Isolated;

internal sealed class IsolatedDictionary<TValue> where TValue : class
{
    private ConcurrentDictionary<string, TValue> _dictionary = new();

    private readonly ConcurrentDictionary<string, Transaction<TValue>> _transactions = new();

    internal Dictionary<string, WriteOperation<TValue>> SaveChanges(string transactionId)
    {
        var transaction = _transactions[transactionId];

        _transactions.Remove(transaction.Id, out _);

        transaction.Apply();

        var operations = transaction.GetOperations();

        transaction.StopProcessing();

        _dictionary = new ConcurrentDictionary<string, TValue>(transaction.Snapshot);

        return operations;
    }

    internal int Count()
    {
        return _dictionary.Count;
    }

    public TValue? Get(string key, string transactionId)
    {
        var transaction = _transactions[transactionId];

        return transaction.Get(key);
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

    internal void UpdateTransactionWithLog(Dictionary<string, WriteOperation<TValue>> log, string transactionId, long lastLogTime)
    {
        var transaction = _transactions[transactionId];

        transaction.LazySync(log, lastLogTime);
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
}