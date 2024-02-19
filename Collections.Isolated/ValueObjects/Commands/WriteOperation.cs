namespace Collections.Isolated.ValueObjects.Commands;

internal abstract record WriteOperation<TValue>(string Key, long CreationTime) : Operation(CreationTime)
    where TValue : class
{
    internal abstract void Apply(IDictionary<string, TValue> dictionary);

    internal abstract WriteOperation<TValue> LazyDeepCloneValue();
}