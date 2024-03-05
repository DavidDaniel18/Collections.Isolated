using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Dictionary.ValueObjects;

namespace Collections.Isolated.Application.Synchronisation;

internal sealed class DistributedDecorator<TValue>(IHostInfo hostInfo, IIsolatedDictionary<TValue> decoratedDictionary, ILogClient logClient) : IIsolatedDictionary<TValue> where TValue : class
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

    public Task<IEnumerable<TValue>> GetAllAsync(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }
}