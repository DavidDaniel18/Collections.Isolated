using Force.DeepCloner;
using MessagePack;

namespace Collections.Isolated.Serialization;

internal static class Serializer
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

    internal static T DeepClone<T>(T obj)
    {
        return obj.DeepClone();
    }

    internal static byte[] SerializeToBytes<T>(T obj)
    {
        return MessagePackSerializer.Serialize(obj, Options);
    }

    internal static T DeserializeFromBytes<T>(byte[] data)
    {
        return MessagePackSerializer.Deserialize<T>(data, Options);
    }

    internal static bool IsPrimitiveOrSpecialType<T>()
    {
        Type type = typeof(T);
        return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
    }

}