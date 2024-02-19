namespace Collections.Isolated.ValueObjects.Commands;

internal abstract record WriteOperation<TValue>(string Key, DateTime DateTime) : Operation(DateTime)
    where TValue : class
{
    internal abstract void Apply(IDictionary<string, TValue> dictionary);

    internal abstract WriteOperation<TValue> LazyDeepCloneValue();
}