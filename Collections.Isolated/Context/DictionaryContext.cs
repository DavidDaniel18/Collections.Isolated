using Collections.Isolated.Abstractions;
using Collections.Isolated.Entities;
using Collections.Isolated.Enums;
using Collections.Isolated.Interfaces;

namespace Collections.Isolated.Context;

public sealed class DictionaryContext<TValue>(IIsolatedDictionary<TValue> dictionary) : IDictionaryContext<TValue>, IDisposable
    where TValue : class
{
    private IntentionLock _intentionLock = new (
        Guid.NewGuid().ToString(),
        [],
        Intent.Write,
        new TaskCompletionSource<bool>());

    private bool _disposed;

    public void StateIntent(IEnumerable<string> keys, bool readOnly)
    {
        _intentionLock = _intentionLock with
        {
            KeysToLock = keys.ToArray(), Intent = readOnly ? Intent.Read : Intent.Write
        };
    }

    public async Task AddOrUpdateAsync(string key, TValue value)
    {
        RenewTransaction();

        await dictionary.AddOrUpdateAsync(key, value, _intentionLock);
    }

    public async Task RemoveAsync(string key)
    {
        RenewTransaction();

        await dictionary.RemoveAsync(key, _intentionLock);
    }

    public async Task<int> CountAsync()
    {
        RenewTransaction();

        return await dictionary.CountAsync(_intentionLock);
    }

    public async Task<TValue?> TryGetAsync(string key)
    {
        RenewTransaction();

        return await dictionary.GetAsync(key, _intentionLock);
    }

    public async Task SaveChangesAsync()
    {
        RenewTransaction();

        await dictionary.SaveChangesAsync(_intentionLock);

        _disposed = true;
    }

    public void Dispose()
    {
        dictionary.UndoChanges(_intentionLock);

        _disposed = true;
    }

    private void RenewTransaction()
    {
        if (_disposed)
        {
            _disposed = false;
            _intentionLock = _intentionLock with { TaskCompletionSource = new TaskCompletionSource<bool>() };
        }
    }
}