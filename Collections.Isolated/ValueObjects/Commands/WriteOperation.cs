using System.Collections.Concurrent;
using System.Collections.Immutable;
using Collections.Isolated.Monads;

namespace Collections.Isolated.ValueObjects.Commands;

public abstract class WriteOperation<TKey, TValue> : Operation<TKey, TValue>
    where TValue : class 
    where TKey : notnull
{
    public abstract Result Apply(ConcurrentDictionary<TKey, TValue> dictionary);

    public abstract bool IsKeyColliding(ImmutableHashSet<TKey> lockedKeys);

    public abstract List<TKey> GetKeys();
}