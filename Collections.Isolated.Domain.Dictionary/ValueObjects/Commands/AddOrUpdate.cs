namespace Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

internal sealed record AddOrUpdate : WriteOperation
{
    internal byte[] Bytes { get; }

    internal AddOrUpdate(string key, byte[] value, long creationTime) : base(key, creationTime)
    {
        Bytes = value;
    }

    internal override void Apply(IDictionary<string, byte[]> dictionary)
    {
        dictionary[Key] = Bytes;

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
