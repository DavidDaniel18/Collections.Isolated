
using FastDeepCloner;

namespace Collections.Isolated.Domain.Dictionary.Serialization;

internal static class Serializer
{
    internal static T DeepClone<T>(T obj)
    {
        return (T)obj.Clone(FieldType.Both);
    }

    internal static bool IsPrimitiveOrSpecialType<T>()
    {
        Type type = typeof(T);
        return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
    }

}