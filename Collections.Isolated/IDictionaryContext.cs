namespace Collections.Isolated;

/// <summary>
/// The DictionaryContext acts as a transactional wrapper around the IIsolatedDictionary. Providing a Unit of Work pattern.
/// This class is not thread safe. It is designed to be used in a scoped context.
/// It is important to call SaveChangesAsync to persist changes to the dictionary.
/// </summary>
/// <typeparam name="TValue">The type of value to store.</typeparam>
public interface IDictionaryContext<TValue>
    where TValue : class
{
    /// <summary>
    /// Locks the dictionary for the given keys with the given intention. Significant performance improvements can be made by using this method.
    /// Avoids the need to lock the entire dictionary.
    /// May only be called once per transaction.
    /// </summary>
    /// <param name="keys">The intented keys to lock for the duration of this transaction</param>
    /// <param name="readOnly">Keys in Readonly necessitate lighter locking than those with Write intent</param>
    /// <exception cref="InvalidOperationException">Throws when <see cref="StateIntent"/> is called more than once during the lifetime of a transaction</exception>
    void StateIntent(IEnumerable<string> keys, bool readOnly);

    /// <summary>
    /// Adds or updates a value in the dictionary. Async since it may require a lock.
    /// </summary>
    /// <param name="key">String key to add or update</param>
    /// <param name="value">Value to add or update</param>
    /// <exception cref="InvalidOperationException">Throws when <see cref="StateIntent"/> is more restrictive than the provided key and operation</exception>
    Task AddOrUpdateAsync(string key, TValue value);

    /// <summary>
    /// Removes a value from the dictionary if it is present. Async since it may require a lock.
    /// </summary>
    /// <param name="key">string key to look up</param>
    /// <exception cref="InvalidOperationException">Throws when <see cref="StateIntent"/> is more restrictive than the provided key and operation</exception>
    Task RemoveAsync(string key);

    /// <summary>
    /// Returns the number of elements in the dictionary. Async since it may require a lock.
    /// May be very slow if there is contention since it locks the entire dictionary.
    /// Note: This method creates a transaction and locks the entire dictionary.
    /// </summary>
    /// <exception cref="InvalidOperationException">Throws when <see cref="StateIntent"/> is more restrictive than the provided key and operation</exception>
    Task<int> CountAsync();

    /// <summary>
    /// Gets a value from the dictionary if it is present. Async since it may require a lock.
    /// Gives deep copies of the main storage, avoiding mutations by reference. Local transactions are not deep copied.
    /// </summary>
    /// <param name="key">String key to look up</param>
    Task<TValue?> TryGetAsync(string key);

    /// <summary>
    /// Persists changes to the dictionary. Async since it may require a lock.
    /// Resets the transaction.
    /// </summary>
    Task SaveChangesAsync();

    /// <summary>
    /// Undo changes to the dictionary made during this transaction. Resets the transaction.
    /// Automatically called when the transaction is disposed, usually when the scope is disposed.
    /// Can be called manually to improve performance or to retry a transaction.
    /// </summary>
    void RollBack();
}