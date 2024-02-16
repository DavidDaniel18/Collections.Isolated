namespace Collections.Isolated.ValueObjects.Query;

public sealed class QueryKey<TKey, TValue> : ReadOperation<TKey, TValue>
    where TValue : class
    where TKey : notnull
{
    public QueryKey(TKey key)
    {
        Key = key;
    }

    public TKey Key { get; set; }
}