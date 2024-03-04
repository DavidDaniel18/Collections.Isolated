using Collections.Isolated.Application.Interfaces;
using Grpc.Net.Client;
using System.Collections.Immutable;

namespace Collections.Isolated.Infrastructure;

internal sealed class GrpcClients
{
    internal static GrpcClients? instance;

    internal ImmutableList<GrpcChannel> Channels { get; private set; }

    internal GrpcClients(IHostInfo hostInfo)
    {
        var channelBuilder = ImmutableList.CreateBuilder<GrpcChannel>();

        foreach (var address in hostInfo.GetPairAddresses())
        {
            channelBuilder.Add(GrpcChannel.ForAddress(address));
        }

        Channels = channelBuilder.ToImmutable();
    }
}