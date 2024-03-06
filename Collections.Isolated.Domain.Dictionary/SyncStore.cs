using System.Collections.Concurrent;
using Collections.Isolated.Domain.Dictionary.Entities;
using Collections.Isolated.Domain.Dictionary.Interfaces;
using Collections.Isolated.Domain.Dictionary.Serialization;
using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Domain.Dictionary;

internal sealed class SyncStore<TValue>(ILog<TValue> log) : ISyncStore<TValue>
{
    private readonly ConcurrentDictionary<string, byte[]> _dictionary = new();

    private readonly ConcurrentDictionary<string, Transaction> _transactions = new();

    public void SaveChanges(string transactionId)
    {
        var transaction = _transactions[transactionId];

        _transactions.Remove(transaction.Id, out _);

        var operations = transaction.GetOperations();

        foreach (var operation in operations.Values)
        {
            operation.Apply(_dictionary);
        }

        log.UpdateLog(operations);
    }

    public int Count()
    {
        return _dictionary.Count;
    }

    public TValue? Get(string key, string transactionId)
    {
        var transaction = _transactions[transactionId];

        var bytes =transaction.Get(key);

        if (bytes == null)
        {
            _dictionary.TryGetValue(key, out bytes);
        }

        return bytes is null ? default : Serializer.Deserialize<TValue>(bytes);
    }

    public void AddOrUpdate(string key, TValue value, string transactionId)
    {
        var transaction = _transactions[transactionId];

        var bytes = Serializer.Serialize(value);

        transaction.AddOrUpdateOperation(key, bytes);
    }

    public void Remove(string key, string transactionId)
    {
        var transaction = _transactions[transactionId];

        transaction.AddRemoveOperation(key);
    }

    public void UndoChanges(string transactionId)
    {
        if (_transactions.TryRemove(transactionId, out var transaction))
        {
            transaction.Clear();
        }
    }

    public void EnsureTransactionCreated(IntentionLock transactionLock)
    {
        if(ContainsTransaction(transactionLock.TransactionId) is false)
        {
            CreateTransaction(transactionLock);
        }
    }

    public bool ContainsTransaction(string transactionId)
    {
        return _transactions.ContainsKey(transactionId);
    }

    public void UpdateTransactionWithLog(string transactionId, IEnumerable<WriteOperation> log, long lastLogTime)
    {
        var transaction = _transactions[transactionId];

        transaction.Sync(log, lastLogTime);
    }

    private void CreateTransaction(IntentionLock transactionLock)
    {
        var snapshot = new Dictionary<string, byte[]>();

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

        var transaction = new Transaction(transactionLock.TransactionId, Clock.GetTicks());

        _transactions.TryAdd(transactionLock.TransactionId, transaction);
    }

    public List<TValue> GetTrackedEntities(string intentionLockTransactionId)
    {
        var bytes = _transactions[intentionLockTransactionId].GetTrackedEntities();

        return bytes.Select(Serializer.Deserialize<TValue>).ToList();
    }

    public List<TValue> GetAll(string intentionLockTransactionId)
    {
        var operations = _transactions[intentionLockTransactionId].GetOperations();

        var newDictionary = new Dictionary<string, byte[]>(_dictionary);

        foreach (var operation in operations.Values)
        {
            operation.Apply(newDictionary);
        }

        return newDictionary.Values.Select(Serializer.Deserialize<TValue>).ToList();
    }
}