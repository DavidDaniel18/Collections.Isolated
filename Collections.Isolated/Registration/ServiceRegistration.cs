using Collections.Isolated.Abstractions;
using Collections.Isolated.Context;
using Collections.Isolated.Interfaces;
using Collections.Isolated.Synchronisation;
using Microsoft.Extensions.DependencyInjection;

namespace Collections.Isolated.Registration;

public static class ServiceRegistration
{
    public static IServiceCollection AddIsolatedCollections(this IServiceCollection collection)
    {
        collection.AddSingleton(typeof(IIsolatedDictionary<>), typeof(IsolationSync<>));
        collection.AddScoped(typeof(IDictionaryContext<>), typeof(DictionaryContext<>));

        return collection;
    }
}