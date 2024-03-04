using Raft;

namespace Collections.Isolated.Application.Interfaces;

internal interface IRaftElectionClient
{
    Task<IEnumerable<ProposalReply>> RequestVotes(Proposal proposal, CancellationToken cancellationToken);
}