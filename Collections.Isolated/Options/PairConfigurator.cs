using System.Collections.Immutable;
using Collections.Isolated.Abstractions;

namespace Collections.Isolated.Options;

internal sealed class PairConfigurator : IPairConfigurator
{
    public ImmutableList<Target> Targets = ImmutableList<Target>.Empty;

    public void AddPair(string host, int port)
    {
        Targets = Targets.Add(new Target { Host = host, Port = port });
    }

    public class Target
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 0;
    }
}