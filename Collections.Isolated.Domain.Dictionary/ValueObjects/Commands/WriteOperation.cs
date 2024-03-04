namespace Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

internal abstract record WriteOperation<TValue>(string Key, long CreationTime) : Operation(CreationTime)
    where TValue : class
{
    internal abstract void Apply(IDictionary<string, TValue> dictionary);
}