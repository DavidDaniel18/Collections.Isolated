using System.Collections.Concurrent;
using Collections.Isolated.Entities;
using Collections.Isolated.Serialization;

namespace Collections.Isolated;

internal sealed class IsolatedDictionary<TValue> where TValue : class
{
    private ConcurrentDictionary<string, TValue> _dictionary = new();

    internal readonly ConcurrentDictionary<string, Transaction<TValue>> _transactions = new();

    internal void SaveChanges(Transaction<TValue> transaction)
    {
        _transactions.Remove(transaction.Id, out _);

        transaction.Apply();

        transaction.StopProcessing();

        _dictionary = new ConcurrentDictionary<string, TValue>(transaction.Snapshot);
    }

    internal int Count()
    {
        return _dictionary.Count;
    }

    internal void UndoChanges(string transactionId)
    {
        if (_transactions.TryRemove(transactionId, out var transaction))
        {
            transaction.Clear();
        }
    }

    internal Transaction<TValue> GetOrCreateTransaction(string transactionId)
    {
        return GetTransaction(transactionId) ?? CreateTransaction(transactionId);
    }

    internal Transaction<TValue>? GetTransaction(string transactionId)
    {
        _transactions.TryGetValue(transactionId, out var transaction);

        return transaction;
    }

    private Transaction<TValue> CreateTransaction(string transactionId)
    {
        var creationTime = DateTime.UtcNow;

        var snapshot = new Dictionary<string, TValue>();

        foreach (var (key, value) in _dictionary)
        {
            snapshot.Add(key, DeepCloneValue(value));
        }

        var transaction = new Transaction<TValue>(transactionId, new Dictionary<string, TValue>(snapshot), creationTime);

        _transactions.TryAdd(transactionId, transaction);

        return transaction;
    }

    private TValue DeepCloneValue(TValue value)
    {
        if(Serializer.IsPrimitiveOrSpecialType<TValue>()) return value;

        return Serializer.DeserializeFromBytes<TValue>(Serializer.SerializeToBytes(value));
    }

    public Dictionary<string, Transaction<TValue>> GetTransactions()
    {
        return _transactions.ToDictionary();
    }
}