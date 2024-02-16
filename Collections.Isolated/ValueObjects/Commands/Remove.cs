using System.Collections.Concurrent;
using System.Collections.Immutable;
using Collections.Isolated.Monads;

namespace Collections.Isolated.ValueObjects.Commands;

public sealed class Remove<TKey, TValue> : WriteOperation<TKey, TValue>
    where TValue : class
    where TKey : notnull
{
    internal readonly TKey Key;

    public Remove(TKey key)
    {
        this.Key = key;
    }

    public override List<TKey> GetKeys()
    {
        return new List<TKey> { Key };
    }

    public override Result Apply(ConcurrentDictionary<TKey, TValue> dictionary)
    {
        return dictionary.TryRemove(Key, out _)
            ? Result.Success()
            : Result.Failure($"Aggregate {typeof(TValue).Name} with id {Key} not found.");
    }

    public override bool IsKeyColliding(ImmutableHashSet<TKey> lockedKeys)
    {
        return lockedKeys.Contains(Key);
    }
}