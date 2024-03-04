namespace Collections.Isolated.Abstractions;

public interface ICollectionIsolatedConfigurator
{
    void AddStore<TKey, TValue>()
        where TValue : class
        where TKey : notnull;

    void AddPairs(Action<IPairConfigurator> configurator);
}