namespace Collections.Isolated.Abstractions;

public interface ICollectionIsolatedConfigurator
{
    void AddStore<TValue>();

    void AddPairs(Action<IPairConfigurator> configurator);

    int TransactionTimeoutInMs { get; set; }
}