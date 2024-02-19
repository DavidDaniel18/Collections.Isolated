using Collections.Isolated.Abstractions;
using Collections.Isolated.Interfaces;

namespace Collections.Isolated.Context;

public sealed class DictionaryContext<TValue> : IDictionaryContext<TValue>, IDisposable
    where TValue : class
{
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly IIsolatedDictionary<TValue> _dictionary;

    public DictionaryContext(IIsolatedDictionary<TValue> dictionary)
    {
        _dictionary = dictionary;
    }

    public async Task AddOrUpdateAsync(string key, TValue value)
    {
        await _dictionary.AddOrUpdateAsync(key, value, _id);
    }

    public async Task AddOrUpdateRangeAsync(IEnumerable<(string key, TValue value)> items)
    {
        await _dictionary.BatchApplyOperationAsync(items, _id);
    }

    public async Task RemoveAsync(string key)
    {
        await _dictionary.RemoveAsync(key, _id);
    }

    public async Task<int> CountAsync()
    {
        return await _dictionary.CountAsync(_id);
    }

    public async Task<TValue?> TryGetAsync(string key)
    {
        return await _dictionary.GetAsync(key, _id);
    }

    public async Task SaveChangesAsync()
    {
        await _dictionary.SaveChangesAsync(_id);
    }

    public void Dispose()
    {
        _dictionary.UndoChanges(_id);
    }
}