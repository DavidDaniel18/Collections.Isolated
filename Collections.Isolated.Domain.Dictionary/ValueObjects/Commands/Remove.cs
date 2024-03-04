namespace Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

internal sealed record Remove<TValue>(string Key, long CreationTime) : WriteOperation<TValue>(Key, CreationTime) where TValue : class
{
    internal override void Apply(IDictionary<string, TValue> dictionary)
    {
        dictionary.Remove(Key);
    }
}