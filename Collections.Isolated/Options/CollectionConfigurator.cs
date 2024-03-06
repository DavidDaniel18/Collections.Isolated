using Collections.Isolated.Abstractions;

namespace Collections.Isolated.Options;

internal sealed class CollectionConfigurator : ICollectionIsolatedConfigurator
{
    internal List<Type> Types { get; set; } = new();

    internal PairConfigurator PairConfigurator { get; private set; } = new();

    public int ExposedPort { get; set; }

    public void AddStore<TValue>()
    {
        Types.Add(typeof(TValue));
    }

    public void AddPairs(Action<IPairConfigurator> configurator)
    {
        var pairConfigurator = new PairConfigurator();

        configurator(pairConfigurator);

        PairConfigurator = pairConfigurator;
    }

    public int TransactionTimeoutInMs { get; set; }
}