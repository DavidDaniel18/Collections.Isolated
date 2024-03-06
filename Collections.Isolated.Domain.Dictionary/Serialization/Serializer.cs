using MessagePack.Resolvers;
using MessagePack;

namespace Collections.Isolated.Domain.Dictionary.Serialization;

internal static class Serializer
{
    static Serializer()
    {
        // Setup options with contractless resolver for flexibility
        // You can customize this to use other resolvers or options as needed
        var options = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
        MessagePackSerializer.DefaultOptions = options;
    }

    internal static byte[] Serialize<T>(T obj)
    {
        return MessagePack.MessagePackSerializer.Serialize(obj);
    }

    internal static T Deserialize<T>(byte[] bytes)
    {
        return MessagePack.MessagePackSerializer.Deserialize<T>(bytes);
    }
}