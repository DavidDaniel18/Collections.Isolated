namespace Collections.Isolated.ValueObjects.Query;

public abstract class ReadOperation<TKey, TValue> : Operation<TKey, TValue>
    where TValue : class 
    where TKey : notnull
{
}