namespace Collections.Isolated.Abstractions;

public interface IDictionaryContext<TValue>
    where TValue : class
{
    void AddOrUpdate(string key, TValue value);

    void AddOrUpdateRange(IEnumerable<(string key, TValue value)> items);

    void Remove(string key);

    Task<int> CountAsync();

    Task<TValue?> TryGetAsync(string key);

    Task SaveChangesAsync();
}