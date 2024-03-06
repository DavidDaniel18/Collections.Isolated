using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Dictionary.Interfaces;
using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Application.Decorators.SyncStore;

internal sealed class LeaderLockingDecorator<TValue>(
    ISelectiveReleaseAsync<TValue> selectiveRelease,
    ILog<TValue> log,
    ISyncStoreAsync<TValue> dictionary) : ISyncStoreAsync<TValue>
{
    public async Task<TValue?> GetAsync(string key, IntentionLock intentionLock)
    {
        await dictionary.EnsureTransactionCreatedAsync(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        return await dictionary.GetAsync(key, intentionLock);
    }

    public async Task AddOrUpdateAsync(string key, TValue value, IntentionLock intentionLock)
    {
        await dictionary.EnsureTransactionCreatedAsync(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        await dictionary.AddOrUpdateAsync(key, value, intentionLock);
    }

    public async Task RemoveAsync(string key, IntentionLock intentionLock)
    {
        await dictionary.EnsureTransactionCreatedAsync(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        await dictionary.RemoveAsync(key, intentionLock);
    }

    public async Task<int> CountAsync(IntentionLock intentionLock)
    {
        await dictionary.EnsureTransactionCreatedAsync(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        return await dictionary.CountAsync(intentionLock);
    }

    public async Task SaveChangesAsync(IntentionLock intentionLock)
    {
        if (dictionary.ContainsTransaction(intentionLock.TransactionId) is false) return;

        await AttemptLockAcquisition(intentionLock);

        await dictionary.SaveChangesAsync(intentionLock);

        await selectiveRelease.ReleaseAsync(intentionLock);
    }

    public async Task UndoChangesAsync(IntentionLock intentionLock)
    {
        await selectiveRelease.ReleaseAsync(intentionLock);

        await dictionary.UndoChangesAsync(intentionLock);
    }

    public IEnumerable<TValue> GetTrackedEntities(IntentionLock intentionLock)
    {
        dictionary.EnsureTransactionCreatedAsync(intentionLock);

        return dictionary.GetTrackedEntities(intentionLock);
    }

    public async Task<List<TValue>> GetAllAsync(IntentionLock intentionLock)
    {
        await dictionary.EnsureTransactionCreatedAsync(intentionLock);

        await AttemptLockAcquisition(intentionLock);

        return await dictionary.GetAllAsync(intentionLock);
    }

    public Task EnsureTransactionCreatedAsync(IntentionLock transactionLock)
    {
        return dictionary.EnsureTransactionCreatedAsync(transactionLock);
    }

    public bool ContainsTransaction(string intentionLockTransactionId)
    {
        return dictionary.ContainsTransaction(intentionLockTransactionId);
    }

    public Task UpdateTransactionWithLogAsync(string intentionLockTransactionId, List<WriteOperation> logSnapshot, long lastLogTime)
    {
        return dictionary.UpdateTransactionWithLogAsync(intentionLockTransactionId, logSnapshot, lastLogTime);
    }

    private async Task AttemptLockAcquisition(IntentionLock intentionLock)
    {
        if (await selectiveRelease.NextAcquire(intentionLock))
        {
            await LockAcquired(intentionLock.TransactionId);
        }
    }

    private async Task LockAcquired(string transactionId)
    {
        var lastLogTime = log.GetLastLogTime();

        var logSnapshot = log.GetLog().Values.ToList();

        await dictionary.UpdateTransactionWithLogAsync(transactionId, logSnapshot, lastLogTime);
    }
}