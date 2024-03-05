using Collections.Isolated.Application.Commands.Election;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Raft;
using Election = Raft.Election;

namespace Collections.Isolated.Controllers.Protobuf.Services;

public sealed class ElectionController(IServiceScope serviceScope) : Election.ElectionBase
{
    public override async Task<ProposalReply> Propose(Proposal request, ServerCallContext context)
    {
        var electionProposal = serviceScope.ServiceProvider.GetRequiredService<ElectionProposal>();

        var proposalReply = await electionProposal.Handle(request.NodeId, request.Term);

        return new ProposalReply
        {
            NodeId = proposalReply.NodeId,
            Term = proposalReply.Term,
            IssuerId = proposalReply.IssuerId
        };
    }
}