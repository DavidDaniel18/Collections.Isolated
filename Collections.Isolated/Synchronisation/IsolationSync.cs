using Collections.Isolated.Entities;
using Collections.Isolated.Interfaces;
using Collections.Isolated.ValueObjects.Commands;

namespace Collections.Isolated.Synchronisation;

public sealed class IsolationSync<TValue> : IIsolatedDictionary<TValue>
    where TValue : class
{
    private readonly IsolatedDictionary<TValue> _dictionary = new();

    private Dictionary<string, WriteOperation<TValue>> _log = new();

    private readonly SelectiveRelease _selectiveRelease = new();

    private long _lastLogTime = -1;

    public async Task<TValue?> GetAsync(string key, string transactionId)
    {
        _dictionary.EnsureTransactionCreated(transactionId);

        await AttemptLockAcquisition(transactionId);

        return _dictionary.Get(key, transactionId);
    }

    public void AddOrUpdate(string key, TValue value, string transactionId)
    {
        _dictionary.EnsureTransactionCreated(transactionId);

        _dictionary.AddOrUpdate(key, value, transactionId);
    }

    public void Remove(string key, string transactionId)
    {
        _dictionary.EnsureTransactionCreated(transactionId);

        _dictionary.Remove(key, transactionId);
    }

    public void BatchApplyOperation(IEnumerable<(string key, TValue value)> items, string transactionId)
    {
        _dictionary.EnsureTransactionCreated(transactionId);

        _dictionary.BatchAddOrUpdate(items, transactionId);
    }

    public async Task<int> CountAsync(string transactionId)
    {
        _dictionary.EnsureTransactionCreated(transactionId);

        await AttemptLockAcquisition(transactionId);

        return _dictionary.Count();
    }

    public async Task SaveChangesAsync(string transactionId)
    {
        if (_dictionary.ContainsTransaction(transactionId) is false) return;

        await AttemptLockAcquisition(transactionId);

        var operations = _dictionary.SaveChanges(transactionId);

        _lastLogTime = Clock.GetTicks();

        _log = operations;

        _selectiveRelease.Release();
    }

    public void UndoChanges(string transactionId)
    {
        _dictionary.UndoChanges(transactionId);
    }

    private async Task AttemptLockAcquisition(string transactionId)
    {
        await _selectiveRelease.WaitAsync(transactionId);

        LockAcquired(transactionId);
    }

    private void LockAcquired(string transactionId)
    {
        _dictionary.UpdateTransactionWithLog(_log, transactionId, _lastLogTime);
    }
}