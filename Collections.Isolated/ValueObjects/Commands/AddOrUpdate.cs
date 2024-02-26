using Collections.Isolated.Serialization;

namespace Collections.Isolated.ValueObjects.Commands;

internal sealed record AddOrUpdate<TValue> : WriteOperation<TValue> where TValue : class
{
    internal TValue LazyValue { get; }

    internal AddOrUpdate(string key, TValue value, long creationTime) : base(key, creationTime)
    {
        LazyValue = value;
    }

    internal override void Apply(IDictionary<string, TValue> dictionary)
    {
        if (Serializer.IsPrimitiveOrSpecialType<TValue>())
        {
            dictionary[Key] = LazyValue;
        }
        else
        {
            dictionary[Key] = Serializer.DeepClone(LazyValue);
        }
    }
}
