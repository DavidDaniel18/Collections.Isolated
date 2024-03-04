using System.Collections.Concurrent;
using Collections.Isolated.Domain.Dictionary.Entities;
using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Domain.Dictionary;

internal sealed class IsolatedDictionary<TValue> where TValue : class
{
    private readonly ConcurrentDictionary<string, TValue> _dictionary = new();

    private readonly ConcurrentDictionary<string, Transaction<TValue>> _transactions = new();

    internal IReadOnlyDictionary<string, WriteOperation<TValue>> SaveChanges(string transactionId)
    {
        var transaction = _transactions[transactionId];

        _transactions.Remove(transaction.Id, out _);

        var operations = transaction.GetOperations();

        foreach (var operation in operations.Values)
        {
            operation.Apply(_dictionary);
        }

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

    internal void UndoChanges(string transactionId)
    {
        if (_transactions.TryRemove(transactionId, out var transaction))
        {
            transaction.Clear();
        }
    }

    internal void EnsureTransactionCreated(IntentionLock transactionLock)
    {
        if(ContainsTransaction(transactionLock.TransactionId) is false)
        {
            CreateTransaction(transactionLock);
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

    private void CreateTransaction(IntentionLock transactionLock)
    {
        var snapshot = new Dictionary<string, TValue>();

        if (transactionLock.KeysToLock.Count == 0)
        {
            foreach (var (key, value) in _dictionary)
            {
                snapshot.Add(key, value);
            }
        }
        else
        {
            foreach (var key in transactionLock.KeysToLock)
            {
                snapshot.Add(key, _dictionary[key]);
            }
        }

        var transaction = new Transaction<TValue>(transactionLock.TransactionId, new Dictionary<string, TValue>(snapshot), Clock.GetTicks());

        _transactions.TryAdd(transactionLock.TransactionId, transaction);
    }

    public IEnumerable<TValue> GetTrackedEntities(string intentionLockTransactionId)
    {
        return _transactions[intentionLockTransactionId].GetTrackedEntities();
    }

    public IEnumerable<TValue> GetAll(string intentionLockTransactionId)
    {
        return _transactions[intentionLockTransactionId].GetAll();
    }
}