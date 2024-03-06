using Collections.Isolated.Abstractions;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Context;
using Microsoft.Extensions.DependencyInjection;
using Collections.Isolated.Controllers.Protobuf.Services;
using Collections.Isolated.Domain.Dictionary;
using Collections.Isolated.Domain.Dictionary.Interfaces;
using Collections.Isolated.Domain.Dictionary.Synchronisation;
using Collections.Isolated.Infrastructure;
using Collections.Isolated.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Collections.Isolated.Application.Adaptors;
using Collections.Isolated.Application.Decorators.SyncStore;

namespace Collections.Isolated.Registration;

/// <summary>
/// Registers the Isolated Collections in the Dependency Injection Container
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Adds the Isolated Dictionary to the Dependency Injection Container
    /// </summary>
    /// <param name="collection">The IServiceCollection provided by <see cref="Microsoft.Extensions.DependencyInjection"/></param>
    /// <param name="collectionIsolatedConfigurator">Configurator</param>
    /// <returns></returns>
    public static IServiceCollection AddIsolatedDictionary(this IServiceCollection collection, Action<ICollectionIsolatedConfigurator> collectionIsolatedConfigurator)
    {
        var configurator = new CollectionConfigurator();

        collectionIsolatedConfigurator(configurator);

        foreach (var storeTypes in configurator.Types)
        {
            var genericInterface = typeof(ISyncStoreAsync<>).MakeGenericType(storeTypes);
            var genericIsolatedDictionary = typeof(SyncStore<>).MakeGenericType(storeTypes);
            var genericIsolatedDictionaryAsyncAdaptor = typeof(ApplicationSyncStoreAdaptor<>).MakeGenericType(storeTypes);
            var genericIsolationDecorator = typeof(LeaderLockingDecorator<>).MakeGenericType(storeTypes);

            var genericSelectiveRelease = typeof(ISelectiveRelease<>).MakeGenericType(storeTypes);

            collection.AddSingleton(genericInterface, serviceProvider =>
            {
                var isolatedDictionary = Activator.CreateInstance(genericIsolatedDictionary);
                var isolatedDictionaryAsyncAdaptor = Activator.CreateInstance(genericIsolatedDictionaryAsyncAdaptor, isolatedDictionary);

                var selectiveRelease = serviceProvider.GetRequiredService(genericSelectiveRelease);

                var isolatedDecorator = Activator.CreateInstance(genericIsolationDecorator, selectiveRelease, isolatedDictionaryAsyncAdaptor);

                return isolatedDecorator ?? throw new InvalidOperationException("Could not create IsolatedDictionaryAsyncAdaptor");
            });
        }

        collection.AddSingleton(typeof(ISelectiveRelease<>), typeof(SelectiveRelease<>));
        collection.AddSingleton(typeof(ITransactionSettings), typeof(HostInfo));
        collection.AddScoped(typeof(IDictionaryContext<>), typeof(DictionaryContext<>));

        if (configurator.TransactionTimeoutInMs > 0)
        {
            HostInfo.TransactionTimeoutInMs = configurator.TransactionTimeoutInMs;
        }

        return collection;
    }

    /// <summary>
    /// Add Connections to other hosts to form a distributed key-value store
    /// </summary>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public static IEndpointRouteBuilder MapOtherServices(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<ElectionController>().RequireHost("*:52000");
        endpoints.MapGrpcService<LogController>().RequireHost("*:52000"); ;

        return endpoints;
    }
}