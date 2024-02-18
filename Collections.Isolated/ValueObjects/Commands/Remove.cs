using System.Collections.Concurrent;

namespace Collections.Isolated.ValueObjects.Commands;

internal sealed record Remove<TValue>(string Key, DateTime CreationTime) : WriteOperation<TValue>(Key, CreationTime) where TValue : class
{
    internal override void Apply(ConcurrentDictionary<string, TValue> dictionary)
    {
        dictionary.TryRemove(Key, out _);
    }

    internal override WriteOperation<TValue> LazyDeepCloneValue(DateTime dateTime)
    {
        return new Remove<TValue>(Key, dateTime);
    }
}