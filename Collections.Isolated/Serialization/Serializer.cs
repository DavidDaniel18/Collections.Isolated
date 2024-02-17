using MessagePack;

namespace Collections.Isolated.Serialization;

internal static class Serializer
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

    internal static byte[] SerializeToBytes<T>(T obj)
    {
        return MessagePackSerializer.Serialize(obj, Options);
    }

    internal static T DeserializeFromBytes<T>(byte[] data)
    {
        return MessagePackSerializer.Deserialize<T>(data, Options);
    }
}