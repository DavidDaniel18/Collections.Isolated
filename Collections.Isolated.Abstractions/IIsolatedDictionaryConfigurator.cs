namespace Collections.Isolated.Abstractions;

public interface IIsolatedDictionaryConfigurator
{
    void AddStore<TValue>()
        where TValue : class;
}
