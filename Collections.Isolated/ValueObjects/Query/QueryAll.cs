namespace Collections.Isolated.ValueObjects.Query;

public sealed class QueryAll<TKey, TValue> : ReadOperation<TKey, TValue>
    where TValue : class
    where TKey : notnull
{
}