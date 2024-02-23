using Collections.Isolated.Abstractions;
using Collections.Isolated.Context;
using Collections.Isolated.Interfaces;
using Collections.Isolated.Synchronisation;
using Microsoft.Extensions.DependencyInjection;

namespace Collections.Isolated.Registration;

public static class ServiceRegistration
{
    internal static int TransactionTimeoutInMs = 1_000_000;

    public static IServiceCollection AddIsolatedCollections(this IServiceCollection collection, int transactionTimeoutInMs = -1)
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