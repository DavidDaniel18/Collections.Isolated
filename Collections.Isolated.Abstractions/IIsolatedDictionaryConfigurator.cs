namespace Collections.Isolated.Abstractions;

public interface IIsolatedDictionaryConfigurator
{
    void AddStore<TKey, TValue>()
        where TKey : notnull 
        where TValue : class;
}
