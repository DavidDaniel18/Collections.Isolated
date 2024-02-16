namespace Collections.Isolated.Abstractions;

public interface IDictionaryContext<TKey, TValue>
    where TKey : notnull 
    where TValue : class
{
    Task AddOrUpdateAsync(TKey key, TValue value);
    Task AddOrUpdateRange(IEnumerable<(TKey key, TValue value)> items);
    Task RemoveAsync(TKey key);
    Task<TValue?> TryGetAsync(TKey key);
    Task<List<TValue>> QueryAsync(Func<IEnumerable<TValue>, IEnumerable<TValue>> filter);
    Task SaveChangesAsync();
}