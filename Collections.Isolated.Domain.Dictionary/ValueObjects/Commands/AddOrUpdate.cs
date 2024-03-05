using Collections.Isolated.Domain.Dictionary.Serialization;

namespace Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

internal sealed record AddOrUpdate<TValue> : WriteOperation<TValue> where TValue : class
{
    internal TValue LazyValue { get; }

    internal AddOrUpdate(string key, TValue value, long creationTime) : base(key, creationTime)
    {
        LazyValue = value;
    }

    internal override void Apply(IDictionary<string, TValue> dictionary)
    {
        dictionary[Key] = LazyValue;

        //if (Serializer.IsPrimitiveOrSpecialType<TValue>())
        //{
        //    dictionary[Key] = LazyValue;
        //}
        //else
        //{
        //    dictionary[Key] = Serializer.DeepClone(LazyValue);
        //}
    }
}
