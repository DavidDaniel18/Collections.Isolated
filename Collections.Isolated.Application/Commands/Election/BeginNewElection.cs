using Collections.Isolated.Abstractions;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Consensus;
using Raft;

namespace Collections.Isolated.Application.Commands.Election;

internal sealed class BeginNewElection(IDictionaryContext<Node> dictionaryContext, IRaftElectionClient raftElectionClient, IUnitOfWork<Node> unitOfWork)
{
    internal async Task Handle(string nodeId, CancellationToken cancellationToken)
    {
        var node = await dictionaryContext.TryGetAsync(nodeId);

        if (node is null)
        {
            throw new InvalidOperationException($"Node with id {nodeId} does not exist");
        }

        node.BeginElection();

        var proposal = new Proposal()
        {
            NodeId = node.Id,
            Term = node.GetTerm()
        };

        var votes = await raftElectionClient.RequestVotes(proposal, cancellationToken);

        foreach (var proposalReply in votes)
        {
            node.VoteForCandidate(new Domain.Consensus.ValueObjects.Proposal(proposalReply.Term, proposalReply.NodeId, proposalReply.IssuerId));
        }

        await dictionaryContext.AddOrUpdateAsync(node.Id, node);

        await unitOfWork.SaveChangesAsync();
    }
}