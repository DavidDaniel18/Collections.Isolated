namespace Collections.Isolated.Abstractions;

public interface IDictionaryContext<TValue>
    where TValue : class
{
    void AddOrUpdate(string key, TValue value);

    void AddOrUpdateRange(IEnumerable<(string key, TValue value)> items);

    void Remove(string key);

    int Count();

    TValue? TryGet(string key);

    Task SaveChangesAsync();
}