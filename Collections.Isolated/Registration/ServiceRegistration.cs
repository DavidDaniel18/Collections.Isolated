using Collections.Isolated.Context;
using Collections.Isolated.Interfaces;
using Collections.Isolated.Synchronisation;
using Microsoft.Extensions.DependencyInjection;

namespace Collections.Isolated.Registration;

/// <summary>
/// Registers the Isolated Collections in the Dependency Injection Container
/// </summary>
public static class ServiceRegistration
{
    internal static int TransactionTimeoutInMs = 1_000_000;

    /// <summary>
    /// Adds the Isolated Dictionary to the Dependency Injection Container
    /// </summary>
    /// <param name="collection">The IServiceCollection provided by <see cref="Microsoft.Extensions.DependencyInjection"/></param>
    /// <param name="transactionTimeoutInMs">How long should a transaction wait for a lock before throwing an exception</param>
    /// <returns></returns>
    public static IServiceCollection AddIsolatedDictionary(this IServiceCollection collection, int transactionTimeoutInMs = -1)
    {
        collection.AddSingleton(typeof(IIsolatedDictionary<>), typeof(IsolationSync<>));
        collection.AddScoped(typeof(IDictionaryContext<>), typeof(DictionaryContext<>));

        if (transactionTimeoutInMs > 0)
        {
            TransactionTimeoutInMs = transactionTimeoutInMs;
        }

        return collection;
    }
}