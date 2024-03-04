using System.Collections.Immutable;
using Collections.Isolated.Domain.Dictionary.Interfaces;

namespace Collections.Isolated.Application.Interfaces;

public interface IHostInfo : ITransactionSettings
{
    string GetId();

    string GetLeaderId();

    string GetLeaderKey();

    int GetElectionProposal();

    ImmutableList<string> GetPairKeys();

    IEnumerable<string> GetPairAddresses();

    int GetClusterSize();
}