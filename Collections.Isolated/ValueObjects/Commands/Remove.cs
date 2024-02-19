namespace Collections.Isolated.ValueObjects.Commands;

internal sealed record Remove<TValue>(string Key, DateTime CreationTime) : WriteOperation<TValue>(Key, CreationTime) where TValue : class
{
    internal override void Apply(IDictionary<string, TValue> dictionary)
    {
        dictionary.Remove(Key);
    }

    internal override WriteOperation<TValue> LazyDeepCloneValue()
    {
        return new Remove<TValue>(Key, DateTime);
    }
}