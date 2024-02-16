using Collections.Isolated.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Collections.Isolated.Registration;

public sealed class IsolatedDictionaryConfigurator(IServiceCollection collection) : IIsolatedDictionaryConfigurator
{
    public void AddStore<TKey, TValue>() 
        where TKey : notnull 
        where TValue : class 
    {
        collection.AddSingleton<IsolatedDictionary<TKey, TValue>>();

        collection.AddScoped<IDictionaryContext<TKey, TValue>, DictionaryContext<TKey, TValue>>();
    }
}