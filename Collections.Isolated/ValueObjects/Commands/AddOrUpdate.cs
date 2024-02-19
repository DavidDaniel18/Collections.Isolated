using Collections.Isolated.Serialization;

namespace Collections.Isolated.ValueObjects.Commands;

internal sealed record AddOrUpdate<TValue> : WriteOperation<TValue> where TValue : class
{
    internal Lazy<TValue> LazyValue { get; private set; }

    internal AddOrUpdate(string key, TValue value, DateTime creationTime) : base(key, creationTime)
    {
        LazyValue = new(() => value);

        _ = LazyValue.Value;
    }

    private AddOrUpdate(string key, Lazy<TValue> lazyValue, DateTime creationTime) : base(key, creationTime)
    {
        LazyValue = lazyValue;
    }

    internal override void Apply(IDictionary<string, TValue> dictionary)
    {
        dictionary[Key] =  LazyValue.Value;
    }

    internal override WriteOperation<TValue> LazyDeepCloneValue()
    {
        if (Serializer.IsPrimitiveOrSpecialType<TValue>())
        {
            return new AddOrUpdate<TValue>(Key, LazyValue.Value, DateTime);
        }

        var lazyValue = new Lazy<TValue>(() => Serializer.DeserializeFromBytes<TValue>(Serializer.SerializeToBytes(LazyValue)));

        return new AddOrUpdate<TValue>(Key, lazyValue, DateTime);
    }
}