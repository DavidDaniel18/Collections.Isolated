using Collections.Isolated.Entities;
using Collections.Isolated.Interfaces;
using Collections.Isolated.ValueObjects.Commands;

namespace Collections.Isolated.Synchronisation;

internal sealed class IsolationSync<TValue> : IIsolatedDictionary<TValue> where TValue : class
{
    private readonly IsolatedDictionary<TValue> _dictionary = new();

    private volatile IReadOnlyDictionary<string, WriteOperation<TValue>> _log = new Dictionary<string, WriteOperation<TValue>>();

    private readonly SelectiveRelease _selectiveRelease = new();

    private long _lastLogTime = -1;

    public async Task<TValue?> GetAsync(string key, IntentionLock intentionLock)
    {
        _dictionary.EnsureTransactionCreated(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        return _dictionary.Get(key, intentionLock.TransactionId);
    }

    public async Task AddOrUpdateAsync(string key, TValue value, IntentionLock intentionLock)
    {
        _dictionary.EnsureTransactionCreated(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        _dictionary.AddOrUpdate(key, value, intentionLock.TransactionId);
    }

    public async Task RemoveAsync(string key, IntentionLock intentionLock)
    {
        _dictionary.EnsureTransactionCreated(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        _dictionary.Remove(key, intentionLock.TransactionId);
    }

    public async Task<int> CountAsync(IntentionLock intentionLock)
    {
        _dictionary.EnsureTransactionCreated(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        return _dictionary.Count();
    }

    public async Task SaveChangesAsync(IntentionLock intentionLock)
    {
        if (_dictionary.ContainsTransaction(intentionLock.TransactionId) is false) return;

        await AttemptLockAcquisition(intentionLock);

        Interlocked.Exchange(ref _log, _dictionary.SaveChanges(intentionLock.TransactionId));

        Interlocked.Exchange(ref _lastLogTime, Clock.GetTicks());

        _selectiveRelease.Release(intentionLock);
    }

    public void UndoChanges(IntentionLock intentionLock)
    {
        _selectiveRelease.Forfeit(intentionLock);

        _dictionary.UndoChanges(intentionLock.TransactionId);
    }

    private async Task AttemptLockAcquisition(IntentionLock intentionLock)
    {
        if (await _selectiveRelease.NextAcquireAsync(intentionLock))
        {
            LockAcquired(intentionLock.TransactionId);
        }
    }

    private void LockAcquired(string transactionId)
    {
        var lastLogTime = Interlocked.Read(ref _lastLogTime);

        var logSnapshot = _log.Values.ToList();

        _dictionary.UpdateTransactionWithLog(transactionId, logSnapshot, lastLogTime);
    }
}