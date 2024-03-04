using System.Collections.Immutable;
using Collections.Isolated.Application.Interfaces;

namespace Collections.Isolated.Infrastructure;

public sealed class HostInfo : IHostInfo
{
    public static string Id { get; } = Guid.NewGuid().ToString();

    public static string LeaderId { get; set; } = string.Empty;

    public static string LeaderKey { get; set; } = string.Empty;

    public static int ElectionProposal { get; set; } = Random.Shared.Next();

    public static ImmutableList<string> PairKeys { get; } = ImmutableList<string>.Empty;

    public static int TransactionTimeoutInMs { get; set; } = 1_000_000;

    public string GetId() => Id;

    public string GetLeaderId() => LeaderId;

    public string GetLeaderKey() => LeaderKey;

    public int GetElectionProposal() => ElectionProposal;

    public ImmutableList<string> GetPairKeys() => PairKeys;

    public int GetTransactionTimeoutInMs() => TransactionTimeoutInMs;
}