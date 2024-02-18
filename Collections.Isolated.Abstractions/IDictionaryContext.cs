namespace Collections.Isolated.Abstractions;

public interface IDictionaryContext<TValue>
    where TValue : class
{
    Task AddOrUpdateAsync(string key, TValue value);

    Task AddOrUpdateRangeAsync(IEnumerable<(string key, TValue value)> items);

    Task RemoveAsync(string key);

    int Count();

    Task<TValue?> TryGetAsync(string key);

    Task SaveChangesAsync();
}