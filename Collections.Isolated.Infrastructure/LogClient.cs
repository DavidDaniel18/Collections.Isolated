using System.Collections.Immutable;
using Collections.Isolated.Application.Interfaces;
using Grpc.Core;
using Raft;
using static Raft.StateLogger;

namespace Collections.Isolated.Infrastructure;

internal sealed class LogClient : ILogClient
{
    private readonly ImmutableList<StateLoggerClient> _clients;

    internal LogClient(GrpcClients grpcClients)
    {
        var channelBuilder = ImmutableList.CreateBuilder<StateLoggerClient>();

        foreach (var channel in grpcClients.Channels)
        {
            channelBuilder.Add(new StateLoggerClient(channel));
        }

        _clients = channelBuilder.ToImmutable();
    }

    public async Task Log(LogRequests logs, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        foreach (var client in _clients)
        {
            tasks.Add(client.LogAsync(logs, new CallOptions(cancellationToken: cancellationToken)).ResponseAsync);
        }

        await Task.WhenAll(tasks);
    }
}