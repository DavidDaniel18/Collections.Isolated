using Collections.Isolated.Abstractions;
using Collections.Isolated.Entities;
using Collections.Isolated.Enums;
using Collections.Isolated.Interfaces;

namespace Collections.Isolated.Context;

/// <summary>
/// The DictionaryContext acts as a transactional wrapper around the IIsolatedDictionary. Providing a Unit of Work pattern.
/// This class is not thread safe. It is designed to be used in a scoped context.
/// It is important to call SaveChangesAsync to persist changes to the dictionary.
/// </summary>
/// <param name="dictionary">Furfilled by Asp.net Core using dependency injection.</param>
/// <typeparam name="TValue">The type of value to store.</typeparam>
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

    /// <summary>
    /// Locks the dictionary for the given keys with the given intention. Significant performance improvements can be made by using this method.
    /// Avoids the need to lock the entire dictionary.
    /// May only be called once per transaction.
    /// </summary>
    /// <param name="keys">The intented keys to lock for the duration of this transaction</param>
    /// <param name="readOnly">Keys in Readonly necessitate lighter locking than those with Write intent</param>
    /// <exception cref="InvalidOperationException">Throws when <see cref="StateIntent"/> is called more than once during the lifetime of a transaction</exception>

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

    /// <summary>
    /// Adds or updates a value in the dictionary. Async since it may require a lock.
    /// </summary>
    /// <param name="key">String key to add or update</param>
    /// <param name="value">Value to add or update</param>
    /// <exception cref="InvalidOperationException">Throws when <see cref="StateIntent"/> is more restrictive than the provided key and operation</exception>
    public async Task AddOrUpdateAsync(string key, TValue value)
    {
        RenewTransaction();

        ValidateWriteIntent();

        ValideLockedKeysIntent(key);

        await dictionary.AddOrUpdateAsync(key, value, _intentionLock);
    }


    /// <summary>
    /// Removes a value from the dictionary if it is present. Async since it may require a lock.
    /// </summary>
    /// <param name="key">string key to look up</param>
    /// <exception cref="InvalidOperationException">Throws when <see cref="StateIntent"/> is more restrictive than the provided key and operation</exception>
    public async Task RemoveAsync(string key)
    {
        RenewTransaction();

        ValidateWriteIntent();

        ValideLockedKeysIntent(key);

        await dictionary.RemoveAsync(key, _intentionLock);
    }

    /// <summary>
    /// Returns the number of elements in the dictionary. Async since it may require a lock.
    /// May be very slow if there is contention since it locks the entire dictionary.
    /// Note: This method creates a transaction and locks the entire dictionary.
    /// </summary>
    /// <exception cref="InvalidOperationException">Throws when <see cref="StateIntent"/> is more restrictive than the provided key and operation</exception>
    public async Task<int> CountAsync()
    {
        RenewTransaction();

        if(_intentionLock.KeysToLock.Count > 0)
            throw new InvalidOperationException("Cannot count with locked keys.");

        return await dictionary.CountAsync(_intentionLock);
    }

    /// <summary>
    /// Gets a value from the dictionary if it is present. Async since it may require a lock.
    /// Gives deep copies of the main storage, avoiding mutations by reference. Local transactions are not deep copied.
    /// </summary>
    /// <param name="key">String key to look up</param>
    public async Task<TValue?> TryGetAsync(string key)
    {
        RenewTransaction();

        ValideLockedKeysIntent(key);

        return await dictionary.GetAsync(key, _intentionLock);
    }

    /// <summary>
    /// Persists changes to the dictionary. Async since it may require a lock.
    /// Resets the transaction.
    /// </summary>
    public async Task SaveChangesAsync()
    {
        RenewTransaction();

        await dictionary.SaveChangesAsync(_intentionLock);

        _disposed = true;

        _intentStated = false;
    }

    /// <summary>
    /// Undo changes to the dictionary made during this transaction. Resets the transaction.
    /// </summary>
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