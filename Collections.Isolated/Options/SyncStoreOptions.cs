using Collections.Isolated.Abstractions;

namespace Collections.Isolated.Options;

internal sealed class SyncStoreOptions : ICollectionIsolatedConfigurator
{
    internal List<(Type StoreKeyType, Type StoreValueType)> Types { get; set; } = new();

    internal PairConfigurator PairConfigurator { get; private set; } = new();

    public int ExposedPort { get; set; }

    public void AddStore<TKey, TValue>()
    where TValue : class
    where TKey : notnull
    {
        Types.Add((typeof(TKey), typeof(TValue)));
    }

    public void AddPairs(Action<IPairConfigurator> configurator)
    {
        var pairConfigurator = new PairConfigurator();

        configurator(pairConfigurator);

        PairConfigurator = pairConfigurator;
    }
}