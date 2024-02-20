using System.Collections.Immutable;
using Collections.Isolated.Entities;
using Collections.Isolated.Interfaces;
using Collections.Isolated.ValueObjects.Commands;

namespace Collections.Isolated.Synchronisation;

public sealed class IsolationSync<TValue> : IIsolatedDictionary<TValue> where TValue : class
{
    private readonly IsolatedDictionary<TValue> _dictionary = new();

    private volatile Dictionary<string, WriteOperation<TValue>> _log = [];

    private readonly SelectiveRelease _selectiveRelease = new();

    private long _lastLogTime = -1;

    public async Task<TValue?> GetAsync(string key, string transactionId)
    {
        _dictionary.EnsureTransactionCreated(transactionId);

        await AttemptLockAcquisition(transactionId);

        return await _dictionary.GetAsync(key, transactionId);
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

        var operations = await _dictionary.SaveChangesAsync(transactionId);

        Interlocked.Exchange(ref _lastLogTime, Clock.GetTicks());

        _log = operations;

        _selectiveRelease.Release();
    }

    public void UndoChanges(string transactionId)
    {
        _dictionary.UndoChanges(transactionId);
    }

    private async Task AttemptLockAcquisition(string transactionId)
    {
        if (await _selectiveRelease.NextAcquireAsync(transactionId))
        {
            LockAcquired(transactionId);
        }
    }

    private void LockAcquired(string transactionId)
    {
        var logSnapshot = _log.Values.ToList();

        var lastLogTime = Interlocked.Read(ref _lastLogTime);

        _dictionary.UpdateTransactionWithLog(transactionId, logSnapshot, lastLogTime);
    }
}