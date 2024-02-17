using System.Collections.Concurrent;

namespace Collections.Isolated.ValueObjects.Commands;

internal abstract record WriteOperation<TValue>(string Key) : Operation
    where TValue : class
{
    internal abstract void Apply(ConcurrentDictionary<string, TValue> dictionary);
}