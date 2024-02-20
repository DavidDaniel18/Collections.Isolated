using Collections.Isolated.Serialization;

namespace Collections.Isolated.ValueObjects.Commands;

internal sealed record AddOrUpdate<TValue> : WriteOperation<TValue> where TValue : class
{
    internal Lazy<TValue> LazyValue { get; }

    internal AddOrUpdate(string key, TValue value, long creationTime) : base(key, creationTime)
    {
        LazyValue = new Lazy<TValue>(() => value);
    }

    private AddOrUpdate(string key, Lazy<TValue> lazyValue, long creationTime) : base(key, creationTime)
    {
        LazyValue = lazyValue;
    }

    internal override void Apply(IDictionary<string, TValue> dictionary)
    {
        if (Serializer.IsPrimitiveOrSpecialType<TValue>())
        {
            dictionary[Key] = LazyValue.Value;
        }
        else
        {
            dictionary[Key] = Serializer.DeserializeFromBytes<TValue>(Serializer.SerializeToBytes(LazyValue.Value));
        }
    }

    internal override WriteOperation<TValue> LazyDeepCloneValue()
    {
        return this;
        //if (Serializer.IsPrimitiveOrSpecialType<TValue>())
        //{
        //    return new AddOrUpdate<TValue>(Key, LazyValue.Value, CreationTime);
        //}

        //var lazyValue = new Lazy<TValue>(() => Serializer.DeserializeFromBytes<TValue>(Serializer.SerializeToBytes(LazyValue.Value)));

        //return new AddOrUpdate<TValue>(Key, lazyValue, CreationTime);
    }
}
