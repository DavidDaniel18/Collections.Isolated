namespace Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

internal sealed record Remove(string Key, long CreationTime) : WriteOperation(Key, CreationTime)
{
    internal override void Apply(IDictionary<string, byte[]> dictionary)
    {
        dictionary.Remove(Key);
    }
}