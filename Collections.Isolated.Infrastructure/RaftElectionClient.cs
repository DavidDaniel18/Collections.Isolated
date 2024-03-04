using Collections.Isolated.Application.Interfaces;
using System.Collections.Immutable;
using Grpc.Core;
using Raft;

namespace Collections.Isolated.Infrastructure;

internal sealed class RaftElectionClient : IRaftElectionClient
{
    private readonly ImmutableList<Election.ElectionClient> _clients;

    internal RaftElectionClient(GrpcClients grpcClients)
    {
        var channelBuilder = ImmutableList.CreateBuilder<Election.ElectionClient>();

        foreach (var channel in grpcClients.Channels)
        {
            channelBuilder.Add(new Election.ElectionClient(channel));
        }

        _clients = channelBuilder.ToImmutable();
    }

    public async Task<IEnumerable<ProposalReply>> RequestVotes(Proposal proposal, CancellationToken cancellationToken)
    {
        var tasks = _clients.ConvertAll(client => client.ProposeAsync(proposal, new CallOptions(cancellationToken:cancellationToken)).ResponseAsync);

        await Task.WhenAll(tasks);

        return tasks.Select(task => task.Result);
    }
}