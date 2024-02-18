using System.Collections.Concurrent;

namespace Collections.Isolated.ValueObjects.Commands;

internal abstract record WriteOperation<TValue>(string Key, DateTime DateTime) : Operation(DateTime)
    where TValue : class
{
    internal abstract void Apply(ConcurrentDictionary<string, TValue> dictionary);

    internal abstract WriteOperation<TValue> LazyDeepCloneValue(DateTime dateTime);
}