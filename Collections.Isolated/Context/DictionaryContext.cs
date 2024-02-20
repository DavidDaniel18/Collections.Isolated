using Collections.Isolated.Abstractions;
using Collections.Isolated.Interfaces;

namespace Collections.Isolated.Context;

public sealed class DictionaryContext<TValue>(IIsolatedDictionary<TValue> dictionary) : IDictionaryContext<TValue>, IDisposable
    where TValue : class
{
    private readonly string _id = Guid.NewGuid().ToString();

    public void AddOrUpdate(string key, TValue value)
    {
        dictionary.AddOrUpdate(key, value, _id);
    }

    public void AddOrUpdateRange(IEnumerable<(string key, TValue value)> items)
    {
        dictionary.BatchApplyOperation(items, _id);
    }

    public void Remove(string key)
    {
        dictionary.Remove(key, _id);
    }

    public async Task<int> CountAsync()
    {
        return await dictionary.CountAsync(_id);
    }

    public async Task<TValue?> TryGetAsync(string key)
    {
        return await dictionary.GetAsync(key, _id);
    }

    public async Task SaveChangesAsync()
    {
        await dictionary.SaveChangesAsync(_id);
    }

    public void Dispose()
    {
        dictionary.UndoChanges(_id);
    }
}