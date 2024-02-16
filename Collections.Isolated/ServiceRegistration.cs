using Collections.Isolated.Abstractions;
using Collections.Isolated.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Collections.Isolated;

public static class ServiceRegistration
{
    public static IServiceCollection AddIsolatedCollections(this IServiceCollection collection, Action<IIsolatedDictionaryConfigurator> syncStoreConfigure)
    {
        var configuration = new IsolatedDictionaryConfigurator(collection);

        syncStoreConfigure.Invoke(configuration);

        return collection;
    }
}