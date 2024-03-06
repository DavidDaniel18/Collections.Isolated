namespace Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

internal abstract record WriteOperation(string Key, long CreationTime) : Operation(CreationTime)
{
    internal abstract void Apply(IDictionary<string, byte[]> dictionary);
}