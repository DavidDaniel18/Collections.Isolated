using Force.DeepCloner;
using MessagePack;

namespace Collections.Isolated.Serialization;

internal static class Serializer
{
    internal static T DeepClone<T>(T obj)
    {
        return obj.DeepClone();
    }

    internal static bool IsPrimitiveOrSpecialType<T>()
    {
        Type type = typeof(T);
        return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
    }

}