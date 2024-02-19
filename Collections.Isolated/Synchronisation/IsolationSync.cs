using Collections.Isolated.Interfaces;
using System.Collections.Concurrent;
using Collections.Isolated.ValueObjects.Commands;

namespace Collections.Isolated.Synchronisation;

public sealed class IsolationSync<TValue> : IIsolatedDictionary<TValue>
    where TValue : class
{
    private readonly ConcurrentQueue<string> _transactionLockIds = new();

    internal readonly IsolatedDictionary<TValue> _dictionary = new();

    private string _currentTransactionId = string.Empty;

    private Dictionary<string, WriteOperation<TValue>> _log = new();

    private DateTime _lastLogTime = DateTime.UtcNow;

    private readonly SelectiveRelease<TValue> _selectiveRelease;

    public IsolationSync()
    {
        _selectiveRelease = new SelectiveRelease<TValue>(this);
    }

    public async Task<TValue?> GetAsync(string key, string transactionId)
    {
        var transaction = _dictionary.GetOrCreateTransaction(transactionId);

        await AttemptLockAcquisition(transactionId);

        return transaction.Get(key);
    }

    public Task AddOrUpdateAsync(string key, TValue value, string transactionId)
    {
        var transaction = _dictionary.GetOrCreateTransaction(transactionId);

        transaction.AddOrUpdateOperation(key, value);

        return Task.CompletedTask;
    }

    //private void LazyApplyTrailingUpdates()
    //{
    //    var transactions = _dictionary.GetTransactions();

    //    transactions.Remove(_currentTransactionId);

    //    foreach (var transaction in transactions.Values)
    //    {
    //        transaction.();
    //    }
    //}

    public Task RemoveAsync(string key, string transactionId)
    {
        var transaction = _dictionary.GetOrCreateTransaction(transactionId);

        transaction.AddRemoveOperation(key);

        return Task.CompletedTask;
    }

    public Task BatchApplyOperationAsync(IEnumerable<(string key, TValue value)> items, string transactionId)
    {
        var transaction = _dictionary.GetOrCreateTransaction(transactionId);

        transaction.AddOrUpdateBatchOperation(items);

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(string transactionId)
    {
        var transaction = _dictionary.GetTransaction(transactionId);

        if (transaction is null)
        {
            return;
        }

        var operationsToProcess = transaction.GetOperations();

        if (operationsToProcess.Count == 0)
        {
            return;
        }

        await AttemptLockAcquisition(transactionId);

        _dictionary.SaveChanges(transaction);

        _lastLogTime = DateTime.UtcNow;

        _log = operationsToProcess;

        if (_transactionLockIds.TryDequeue(out var nextTransactionId))
        {
            _selectiveRelease.Release(nextTransactionId);
        }
        else
        {
            _currentTransactionId = string.Empty;
        }

        if (_transactionLockIds.Count > 0 && _currentTransactionId == string.Empty)
        {
            throw new InvalidOperationException("There are still transactions in the queue.");
        }
    }

    public async Task<int> CountAsync(string transactionId)
    {
        _ = _dictionary.GetOrCreateTransaction(transactionId);

        await AttemptLockAcquisition(transactionId);

        return _dictionary.Count();
    }

    public void UndoChanges(string transactionId)
    {
        _dictionary.UndoChanges(transactionId);
    }

    private async Task AttemptLockAcquisition(string transactionId)
    {
        if (_currentTransactionId.Equals(transactionId)) return;

        // If the current transaction is the one that is trying to acquire the lock, then we need to wait for the lock to be released.
        if (Interlocked.CompareExchange(ref _currentTransactionId, transactionId, string.Empty).Equals(transactionId)
            || _currentTransactionId.Equals(transactionId) is false)
        {
            _transactionLockIds.Enqueue(transactionId);

            await _selectiveRelease.WaitAsync(transactionId);

            _currentTransactionId = transactionId;
        }

        LockAcquired(transactionId);
    }

    private void LockAcquired(string transactionId)
    {
        var transaction = _dictionary.GetOrCreateTransaction(transactionId);

        transaction.LazySync(_log, _lastLogTime);
    }
}