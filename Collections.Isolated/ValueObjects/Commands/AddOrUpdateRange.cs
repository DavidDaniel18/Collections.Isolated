using System.Collections.Concurrent;
using System.Collections.Immutable;
using Collections.Isolated.Monads;

namespace Collections.Isolated.ValueObjects.Commands;

public sealed class AddOrUpdateRange<TKey, TValue> : WriteOperation<TKey, TValue>
    where TValue : class
    where TKey : notnull
{
    internal readonly IEnumerable<(TKey key, TValue value)> Kvs;

    public AddOrUpdateRange(IEnumerable<(TKey, TValue)> kvs)
    {
        Kvs = kvs;
    }

    public override List<TKey> GetKeys()
    {
        return Kvs.Select(kv => kv.key).ToList();
    }

    public override Result Apply(ConcurrentDictionary<TKey, TValue> dictionary)
    {
        return Result.Foreach(Kvs, kv =>
        {
            dictionary.AddOrUpdate(kv.key, _ => kv.value, (_, _) => kv.value);

            return Result.Success();
        });
    }

    public override bool IsKeyColliding(ImmutableHashSet<TKey> lockedKeys)
    {
        return Kvs.Any(kv => lockedKeys.Contains(kv.key));
    }
}