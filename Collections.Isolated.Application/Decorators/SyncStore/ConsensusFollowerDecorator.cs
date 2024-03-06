using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Application.Decorators.SyncStore;

internal sealed class ConsensusFollowerDecorator<TValue>(ISyncStoreAsync<TValue> syncStoreAsync) : ISyncStoreAsync<TValue>
{
    public Task<TValue?> GetAsync(string key, IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task AddOrUpdateAsync(string key, TValue value, IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task RemoveAsync(string key, IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task SaveChangesAsync(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task<int> CountAsync(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task UndoChangesAsync(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<TValue> GetTrackedEntities(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task<List<TValue>> GetAllAsync(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task EnsureTransactionCreatedAsync(IntentionLock transactionLock)
    {
        throw new NotImplementedException();
    }

    public bool ContainsTransaction(string intentionLockTransactionId)
    {
        throw new NotImplementedException();
    }

    public Task UpdateTransactionWithLogAsync(string intentionLockTransactionId, List<WriteOperation> logSnapshot, long lastLogTime)
    {
        throw new NotImplementedException();
    }
}