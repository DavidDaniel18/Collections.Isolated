using Collections.Isolated.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Collections.Isolated.Registration;

public sealed class IsolatedDictionaryConfigurator(IServiceCollection collection) : IIsolatedDictionaryConfigurator
{
    public void AddStore<TValue>() 
        where TValue : class 
    {
        collection.AddSingleton<IsolatedDictionary<TValue>>();

        collection.AddScoped<IDictionaryContext<TValue>, DictionaryContext<TValue>>();
    }
}