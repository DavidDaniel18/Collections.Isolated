namespace Collections.Isolated.Abstractions;

public interface IDictionaryContext<TValue>
    where TValue : class
{
    void StateIntent(IEnumerable<string> keys, bool readOnly);

    Task AddOrUpdateAsync(string key, TValue value);

    Task RemoveAsync(string key);

    Task<int> CountAsync();

    Task<TValue?> TryGetAsync(string key);

    Task SaveChangesAsync();
}