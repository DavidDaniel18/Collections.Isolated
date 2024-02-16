using System.Collections.Concurrent;
using System.Collections.Immutable;
using Collections.Isolated.Monads;

namespace Collections.Isolated.ValueObjects.Commands;

public sealed class AddOrUpdate<TKey, TValue> : WriteOperation<TKey, TValue>
    where TValue : class
    where TKey : notnull
{
    internal readonly (TKey key, TValue value) Kv;

    public AddOrUpdate((TKey key, TValue value) kv)
    {
        Kv = kv;
    }

    public override List<TKey> GetKeys()
    {
        return new List<TKey> { Kv.key };
    }

    public override Result Apply(ConcurrentDictionary<TKey, TValue> dictionary)
    {
        dictionary.AddOrUpdate(Kv.key, _ => Kv.value, (_, _) =>  Kv.value);

        return Result.Success();
    }

    public override bool IsKeyColliding(ImmutableHashSet<TKey> lockedKeys)
    {
        return lockedKeys.Contains(Kv.key);
    }
}