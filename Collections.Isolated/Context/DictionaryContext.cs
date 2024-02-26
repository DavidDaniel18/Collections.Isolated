using Collections.Isolated.Entities;
using Collections.Isolated.Enums;
using Collections.Isolated.Interfaces;

namespace Collections.Isolated.Context;

/// <inheritdoc cref="IDictionaryContext{TValue}"/>
public sealed class DictionaryContext<TValue>(IIsolatedDictionary<TValue> dictionary) : IDictionaryContext<TValue>, IDisposable
    where TValue : class
{
    private IntentionLock _intentionLock = new (
        Guid.NewGuid().ToString(),
        [],
        Intent.Write,
        new TaskCompletionSource<bool>());

    private bool _disposed;

    private bool _intentStated;

    /// <inheritdoc cref="IDictionaryContext{TValue}.StateIntent"/>
    public void StateIntent(IEnumerable<string> keys, bool readOnly)
    {
        if (_intentStated)
            throw new InvalidOperationException("Cannot state intent more than once per transaction.");

        _intentStated = true;

        _intentionLock = _intentionLock with
        {
            KeysToLock = keys.ToHashSet(), Intent = readOnly ? Intent.Read : Intent.Write
        };
    }

    /// <inheritdoc cref="IDictionaryContext{TValue}.AddOrUpdateAsync"/>
    public async Task AddOrUpdateAsync(string key, TValue value)
    {
        RenewTransaction();

        ValidateWriteIntent();

        ValideLockedKeysIntent(key);

        await dictionary.AddOrUpdateAsync(key, value, _intentionLock);
    }


    /// <inheritdoc cref="IDictionaryContext{TValue}.RemoveAsync"/>
    public async Task RemoveAsync(string key)
    {
        RenewTransaction();

        ValidateWriteIntent();

        ValideLockedKeysIntent(key);

        await dictionary.RemoveAsync(key, _intentionLock);
    }

    /// <inheritdoc cref="IDictionaryContext{TValue}.CountAsync"/>
    public async Task<int> CountAsync()
    {
        RenewTransaction();

        if(_intentionLock.KeysToLock.Count > 0)
            throw new InvalidOperationException("Cannot count with locked keys.");

        return await dictionary.CountAsync(_intentionLock);
    }

    /// <inheritdoc cref="IDictionaryContext{TValue}.TryGetAsync"/>
    public async Task<TValue?> TryGetAsync(string key)
    {
        RenewTransaction();

        ValideLockedKeysIntent(key);

        return await dictionary.GetAsync(key, _intentionLock);
    }

    /// <inheritdoc cref="IDictionaryContext{TValue}.SaveChangesAsync"/>
    public async Task SaveChangesAsync()
    {
        RenewTransaction();

        await dictionary.SaveChangesAsync(_intentionLock);

        _disposed = true;

        _intentStated = false;
    }

    /// <inheritdoc cref="IDictionaryContext{TValue}.RollBack"/>
    public void RollBack()
    {
        Dispose();
    }

    /// <summary>
    /// Disposes of the transaction. Resets the transaction.
    /// </summary>
    /// <remarks>Do not use this if you're name isn't CLR</remarks>
    public void Dispose()
    {
        dictionary.UndoChanges(_intentionLock);

        _disposed = true;

        _intentStated = false;
    }

    private void RenewTransaction()
    {
        if (_disposed)
        {
            _disposed = false;
            _intentionLock = _intentionLock with { TaskCompletionSource = new TaskCompletionSource<bool>() };
        }
    }

    private void ValideLockedKeysIntent(string key)
    {
        if (_intentionLock.KeysToLock.Count > 0 && _intentionLock.KeysToLock.Contains(key) is false)
            throw new InvalidOperationException("Key is not locked for write.");
    }

    private void ValidateWriteIntent()
    {
        if (_intentionLock.Intent is not Intent.Write)
            throw new InvalidOperationException("Cannot write to dictionary with read intention.");
    }

}