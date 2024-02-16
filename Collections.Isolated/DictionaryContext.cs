using Collections.Isolated.Abstractions;
using Collections.Isolated.Monads;
using Collections.Isolated.ValueObjects.Commands;
using Microsoft.Extensions.Logging;

namespace Collections.Isolated;

public sealed class DictionaryContext<TKey, TValue> : IDictionaryContext<TKey, TValue>, IDisposable
    where TKey : notnull
    where TValue : class
{
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly IsolatedDictionary<TKey, TValue> _dictionary;

    public DictionaryContext(IsolatedDictionary<TKey, TValue> dictionary, ILogger<DictionaryContext<TKey, TValue>> logger)
    {
        _dictionary = dictionary;
        Result.Logging ??= logger;
    }

    public async Task AddOrUpdateAsync(TKey key, TValue value)
    {
        var result = await _dictionary.ApplyAsync(new AddOrUpdate<TKey, TValue>((key, value)), _id);

        if (result.IsFailure())
        {
            throw new InvalidOperationException(result.Exception!.Message);
        }
    }

    public async Task AddOrUpdateRange(IEnumerable<(TKey key, TValue value)> items)
    {
        var result = await _dictionary.ApplyAsync(new AddOrUpdateRange<TKey, TValue>(items), _id);

        if (result.IsFailure())
        {
            throw new InvalidOperationException(result.Exception!.Message);
        }
    }

    public async Task RemoveAsync(TKey key)
    {
        var result = await _dictionary.ApplyAsync(new Remove<TKey, TValue>(key), _id);

        if (result.IsFailure())
        {
            throw new InvalidOperationException(result.Exception!.Message);
        }
    }

    public async Task<TValue?> TryGetAsync(TKey key)
    {
        var result = await _dictionary.Get(key, _id);

        return result.IsSuccess()
            ? result.GetValueOrThrow()
            : default;
    }

    public async Task<List<TValue>> QueryAsync(Func<IEnumerable<TValue>, IEnumerable<TValue>> filter)
    {
        var result = await _dictionary.Query(filter, _id);

        if (result.IsFailure())
        {
            throw new InvalidOperationException(result.Exception!.Message);
        }

        return result.GetValueOrThrow();
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