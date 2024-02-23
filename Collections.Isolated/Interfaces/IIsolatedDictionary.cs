using Collections.Isolated.Entities;

namespace Collections.Isolated.Interfaces;

public interface IIsolatedDictionary<TValue> where TValue : class
{
    Task<TValue?> GetAsync(string key, IntentionLock intentionLock);
    Task AddOrUpdateAsync(string key, TValue value, IntentionLock intentionLock);
    Task RemoveAsync(string key, IntentionLock intentionLock);
    Task SaveChangesAsync(IntentionLock intentionLock);
    Task<int> CountAsync(IntentionLock intentionLock);
    void UndoChanges(IntentionLock intentionLock);
}