using System.Collections.Concurrent;

namespace Collections.Isolated.ValueObjects.Commands;

internal sealed record Remove<TValue>(string Key) : WriteOperation<TValue>(Key) where TValue : class
{
    internal override void Apply(ConcurrentDictionary<string, TValue> dictionary)
    {
        dictionary.TryRemove(Key, out _);
    }
}