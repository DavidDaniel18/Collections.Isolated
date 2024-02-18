using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Collections.Isolated.Entities;
using Collections.Isolated.Serialization;
using Collections.Isolated.ValueObjects.Commands;
using Microsoft.Extensions.Logging;

namespace Collections.Isolated;

public sealed class IsolatedDictionary<TValue> where TValue : class
{
    private readonly ILogger<IsolatedDictionary<TValue>> _logger;

    private readonly ConcurrentDictionary<string, TValue> _dictionary = new();

    private readonly ConcurrentDictionary<string, Transaction<TValue>> _transactions = new();

    private readonly ConcurrentQueue<string> _transactionLockIds = new();

    private readonly HashSet<string> _transactionsLocks = new();

    private readonly List<WriteOperation<TValue>> _log = new();

    private string _currentTransactionId = string.Empty;

    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    private DateTime _lastSync = DateTime.UtcNow;

    public IsolatedDictionary(ILogger<IsolatedDictionary<TValue>> logger)
    {
        _logger = logger;
    }

    internal async Task<TValue?> GetAsync(string key, string transactionId)
    {
        var transaction = await PrepareAction(transactionId);

        return transaction.Get(key);
    }

    internal async Task AddOrUpdateAsync(string key, TValue value, string transactionId)
    {
        var transaction = await PrepareAction(transactionId);

        transaction.AddOrUpdateOperation(key, value);
    }

    internal async Task RemoveAsync(string key, string transactionId)
    {
        var transaction = await PrepareAction(transactionId);

        transaction.AddRemoveOperation(key);
    }

    internal async Task BatchApplyOperationAsync(IEnumerable<(string key, TValue value)> items, string transactionId)
    {
        var transaction = await PrepareAction(transactionId);

        transaction.AddOrUpdateBatchOperation(items);
    }

    internal async Task SaveChangesAsync(string transactionId)
    {
        var transaction = GetTransaction(transactionId);

        if (transaction is null)
        {
            throw new InvalidOperationException("Transaction not found");
        }

        await AttemptLockAcquisition(transactionId);

        try
        {
            var operationsToProcess = transaction.GetOperations();

            if (operationsToProcess.Count == 0)
            {
                return;
            }

            _lastSync = operationsToProcess[-0].DateTime;

            _log.AddRange(operationsToProcess);
        }
        finally
        {
            _currentTransactionId = string.Empty;

            _transactions.Remove(transactionId, out _);

            transaction.Apply(_dictionary);
        }
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

    private Transaction<TValue> GetOrCreateTransaction(string transactionId)
    {
        return GetTransaction(transactionId) ?? CreateTransaction(transactionId);
    }

    private Transaction<TValue>? GetTransaction(string transactionId)
    {
        _transactions.TryGetValue(transactionId, out var transaction);

        return transaction;
    }

    private Transaction<TValue> CreateTransaction(string transactionId)
    {
        var snapshot = new Dictionary<string, TValue>();

        foreach (var (key, value) in _dictionary)
        {
            snapshot.Add(key, DeepCloneValue(value));
        }

        _transactions.TryAdd(transactionId, new Transaction<TValue>(transactionId, _logger, new ConcurrentDictionary<string, TValue>(snapshot), _lastSync));

        return _transactions[transactionId];
    }

    private async ValueTask AttemptLockAcquisition(string transactionId)
    {
        if (_currentTransactionId.Equals(transactionId)) return;

        while (_currentTransactionId.Equals(transactionId) is false)
        {
            try
            {
                await _semaphoreSlim.WaitAsync().ConfigureAwait(false);

                if (_transactionsLocks.Add(transactionId))
                {
                    _transactionLockIds.Enqueue(transactionId);
                }

                if (_transactionLockIds.TryPeek(out var lockId))
                {
                    if (Interlocked.CompareExchange(ref _currentTransactionId, lockId, string.Empty).Equals(lockId) is false)
                    {
                        _transactionLockIds.TryDequeue(out _);

                        _transactionsLocks.Remove(lockId);
                    }
                }

                if (_currentTransactionId.Equals(transactionId))
                {
                    return;
                }
            }
            finally
            {
                _semaphoreSlim.Release();

                await Task.Yield();
            }
        }
    }

    private TValue DeepCloneValue(TValue value)
    {
        if(Serializer.IsPrimitiveOrSpecialType<TValue>()) return value;

        return Serializer.DeserializeFromBytes<TValue>(Serializer.SerializeToBytes(value));
    }

    private async Task<Transaction<TValue>> PrepareAction(string transactionId)
    {
        var transaction = GetOrCreateTransaction(transactionId);

        await AttemptLockAcquisition(transactionId);

        SyncLog(transaction);

        return transaction;
    }

    private void SyncLog(Transaction<TValue> transaction)
    {
        var newLog= _log.TakeWhile(op => transaction.CreationTime < op.DateTime);

        transaction.LazySync(newLog);
    }
}