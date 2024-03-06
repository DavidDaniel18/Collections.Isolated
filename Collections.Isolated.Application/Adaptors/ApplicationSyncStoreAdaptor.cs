using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Dictionary.Interfaces;
using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Application.Adaptors;

internal sealed class ApplicationSyncStoreAdaptor<TValue>(ISyncStore<TValue> syncStore) : ISyncStoreAsync<TValue>
{
    public Task<TValue?> GetAsync(string key, IntentionLock intentionLock)
    {
        return Task.FromResult(syncStore.Get(key, intentionLock.TransactionId));
    }

    public Task AddOrUpdateAsync(string key, TValue value, IntentionLock intentionLock)
    {
        syncStore.AddOrUpdate(key, value, intentionLock.TransactionId);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, IntentionLock intentionLock)
    {
        syncStore.Remove(key, intentionLock.TransactionId);

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(IntentionLock intentionLock)
    {
        syncStore.SaveChanges(intentionLock.TransactionId);

        return Task.CompletedTask;
    }

    public Task<int> CountAsync(IntentionLock intentionLock)
    {
        return Task.FromResult(syncStore.Count());
    }

    public Task UndoChangesAsync(IntentionLock intentionLock)
    {
        syncStore.UndoChanges(intentionLock.TransactionId);

        return Task.CompletedTask;
    }

    public IEnumerable<TValue> GetTrackedEntities(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task<List<TValue>> GetTrackedEntitiesAsync(IntentionLock intentionLock)
    {
        return Task.FromResult(syncStore.GetTrackedEntities(intentionLock.TransactionId));
    }

    public Task<List<TValue>> GetAllAsync(IntentionLock intentionLock)
    {
        return Task.FromResult(syncStore.GetAll(intentionLock.TransactionId));
    }

    public bool ContainsTransaction(string intentionLockTransactionId)
    {
        return syncStore.ContainsTransaction(intentionLockTransactionId);
    }

    public Task UpdateTransactionWithLogAsync(string intentionLockTransactionId, List<WriteOperation> logSnapshot, long lastLogTime)
    {
        syncStore.UpdateTransactionWithLog(intentionLockTransactionId, logSnapshot, lastLogTime);

        return Task.CompletedTask;
    }

    public Task EnsureTransactionCreatedAsync(IntentionLock transactionLock)
    {
        syncStore.EnsureTransactionCreated(transactionLock);

        return Task.CompletedTask;
    }
}