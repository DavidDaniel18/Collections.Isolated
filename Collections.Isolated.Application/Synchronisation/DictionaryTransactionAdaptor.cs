using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Dictionary;
using Collections.Isolated.Domain.Dictionary.Entities;
using Collections.Isolated.Domain.Dictionary.Synchronisation;
using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Application.Synchronisation;

internal sealed class DictionaryTransactionAdaptor<TValue>(ISelectiveRelease selectiveRelease) : IIsolatedDictionary<TValue> where TValue : class
{
    private readonly IsolatedDictionary<TValue> _dictionary = new();

    private volatile IReadOnlyDictionary<string, WriteOperation<TValue>> _log = new Dictionary<string, WriteOperation<TValue>>();

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

        await selectiveRelease.ReleaseAsync(intentionLock);
    }

    public async Task UndoChangesAsync(IntentionLock intentionLock)
    {
        await selectiveRelease.ReleaseAsync(intentionLock);

        _dictionary.UndoChanges(intentionLock.TransactionId);
    }

    public IEnumerable<TValue> GetTrackedEntities(IntentionLock intentionLock)
    {
        _dictionary.EnsureTransactionCreated(intentionLock);

        return _dictionary.GetTrackedEntities(intentionLock.TransactionId);
    }

    public async Task<IEnumerable<TValue>> GetAllAsync(IntentionLock intentionLock)
    {
        _dictionary.EnsureTransactionCreated(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        return _dictionary.GetAll(intentionLock.TransactionId);
    }

    private async Task AttemptLockAcquisition(IntentionLock intentionLock)
    {
        if (await selectiveRelease.NextAcquireAsync(intentionLock))
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