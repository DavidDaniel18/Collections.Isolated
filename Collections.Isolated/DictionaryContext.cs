using Collections.Isolated.Abstractions;
using Collections.Isolated.Monads;
using Collections.Isolated.ValueObjects.Commands;
using Microsoft.Extensions.Logging;

namespace Collections.Isolated;

public sealed class DictionaryContext<TValue> : IDictionaryContext<TValue>, IDisposable
    where TValue : class
{
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly IsolatedDictionary<TValue> _dictionary;

    public DictionaryContext(IsolatedDictionary<TValue> dictionary, ILogger<DictionaryContext<TValue>> logger)
    {
        _dictionary = dictionary;
        Result.Logging ??= logger;

        _dictionary.CreateTransaction(_id);
    }

    public void AddOrUpdate(string key, TValue value)
    {
        _dictionary.ApplyOperationAsync(new AddOrUpdate<TValue>(key, value), _id);
    }

    public void AddOrUpdateRange(IEnumerable<(string key, TValue value)> items)
    {
        foreach (var (key, value) in items)
        {
            _dictionary.ApplyOperationAsync(new AddOrUpdate<TValue>(key, value), _id);
        }
    }

    public void Remove(string key)
    {
        _dictionary.ApplyOperationAsync(new Remove<TValue>(key), _id);
    }

    public int Count()
    {
        return _dictionary.Count();
    }

    public TValue? TryGet(string key)
    {
        return _dictionary.Get(key, _id);
    }

    public Task SaveChangesAsync()
    {
        return _dictionary.SaveChangesAsync(_id);
    }

    public void Dispose()
    {
        _dictionary.UndoChanges(_id);
    }
}