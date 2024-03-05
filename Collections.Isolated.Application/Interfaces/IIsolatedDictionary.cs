using Collections.Isolated.Domain.Dictionary.ValueObjects;

namespace Collections.Isolated.Application.Interfaces;

/// <summary>
/// Should not be used directly. Use <see cref="DictionaryContext{TValue}"/> within a scope.
/// </summary>
public interface IIsolatedDictionary<TValue> where TValue : class
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Task<TValue?> GetAsync(string key, IntentionLock intentionLock);
    Task AddOrUpdateAsync(string key, TValue value, IntentionLock intentionLock);
    Task RemoveAsync(string key, IntentionLock intentionLock);
    Task SaveChangesAsync(IntentionLock intentionLock);
    Task<int> CountAsync(IntentionLock intentionLock);
    Task UndoChangesAsync(IntentionLock intentionLock);
    IEnumerable<TValue> GetTrackedEntities(IntentionLock intentionLock);
    Task<IEnumerable<TValue>> GetAllAsync(IntentionLock intentionLock);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}