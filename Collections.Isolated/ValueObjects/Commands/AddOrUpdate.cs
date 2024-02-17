using System.Collections.Concurrent;

namespace Collections.Isolated.ValueObjects.Commands;

internal sealed record AddOrUpdate<TValue>(string Key, TValue Value) : WriteOperation<TValue>(Key) where TValue : class
{
    internal override void Apply(ConcurrentDictionary<string, TValue> dictionary)
    {
        dictionary[Key] =  Value;
    }
}