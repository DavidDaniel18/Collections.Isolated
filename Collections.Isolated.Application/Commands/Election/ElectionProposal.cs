using Collections.Isolated.Abstractions;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Consensus;
using Collections.Isolated.Domain.Consensus.ValueObjects;

namespace Collections.Isolated.Application.Commands.Election;

internal sealed class ElectionProposal(IDictionaryContext<Node> dictionaryContext, IUnitOfWork<Node> unitOfWork)
{
    internal async Task<Proposal> Handle(string proposalIssuerId, int term)
    {
        var node = dictionaryContext.Single();

        var proposalReply = node.OtherNodeRequestOurVote(new Proposal(term, proposalIssuerId, proposalIssuerId));

        await dictionaryContext.AddOrUpdateAsync(node.Id, node);

        await unitOfWork.SaveChangesAsync();

        return proposalReply;
    }
}