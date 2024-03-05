using Collections.Isolated.Abstractions;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Application.Synchronisation;
using Collections.Isolated.Context;
using Microsoft.Extensions.DependencyInjection;
using Collections.Isolated.Controllers.Protobuf.Services;
using Collections.Isolated.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

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
    /// <param name="transactionTimeoutInMs">How long should a transaction wait for a lock before throwing an exception</param>
    /// <returns></returns>
    public static IServiceCollection AddIsolatedDictionary(this IServiceCollection collection, int transactionTimeoutInMs = -1)
    {
        collection.AddSingleton(typeof(IIsolatedDictionary<>), typeof(DictionaryTransactionAdaptor<>));
        collection.AddScoped(typeof(IDictionaryContext<>), typeof(DictionaryContext<>));

        if (transactionTimeoutInMs > 0)
        {
            HostInfo.TransactionTimeoutInMs = transactionTimeoutInMs;
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