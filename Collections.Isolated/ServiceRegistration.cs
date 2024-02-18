using Collections.Isolated.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Collections.Isolated;

public static class ServiceRegistration
{
    public static IServiceCollection AddIsolatedCollections(this IServiceCollection collection)
    {
        collection.AddSingleton(typeof(IsolatedDictionary<>));
        collection.AddScoped(typeof(IDictionaryContext<>), typeof(DictionaryContext<>));

        return collection;
    }
}