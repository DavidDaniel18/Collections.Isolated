using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Application.Interfaces;

internal interface ISyncStoreAsync
{
    Task SaveChangesAsync(IntentionLock intentionLock);
    Task<int> CountAsync(IntentionLock intentionLock);
    Task UndoChangesAsync(IntentionLock intentionLock);
    Task EnsureTransactionCreatedAsync(IntentionLock transactionLock);
    bool ContainsTransaction(string intentionLockTransactionId);
    Task UpdateTransactionWithLogAsync(string intentionLockTransactionId, List<WriteOperation> logSnapshot, long lastLogTime);
}

/// <summary>
/// Should not be used directly. Use <see cref="DictionaryContext{TValue}"/> within a scope.
/// </summary>
internal interface ISyncStoreAsync<TValue> : ISyncStoreAsync
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Task<TValue?> GetAsync(string key, IntentionLock intentionLock);
    Task AddOrUpdateAsync(string key, TValue value, IntentionLock intentionLock);
    Task RemoveAsync(string key, IntentionLock intentionLock);
    IEnumerable<TValue> GetTrackedEntities(IntentionLock intentionLock);
    Task<List<TValue>> GetAllAsync(IntentionLock intentionLock);

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}