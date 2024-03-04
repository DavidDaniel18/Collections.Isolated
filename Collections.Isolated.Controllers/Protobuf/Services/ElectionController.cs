using Collections.Isolated.Application.Commands.Election;
using Grpc.Core;
using Raft;
using Election = Raft.Election;

namespace Collections.Isolated.Controllers.Protobuf.Services;

internal sealed class ElectionController(ElectionProposal electionProposal) : Election.ElectionBase
{
    public override async Task<ProposalReply> Propose(Proposal request, ServerCallContext context)
    {
        var proposalReply = await electionProposal.Handle(request.NodeId, request.Term);

        return new ProposalReply
        {
            NodeId = proposalReply.NodeId,
            Term = proposalReply.Term,
            IssuerId = proposalReply.IssuerId
        };
    }
}