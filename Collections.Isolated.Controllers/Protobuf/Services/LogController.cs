using Collections.Isolated.Application.Commands.Logs;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Raft;

namespace Collections.Isolated.Controllers.Protobuf.Services;

public sealed class LogController(IServiceScope serviceScope) : StateLogger.StateLoggerBase
{
    public override async Task<Empty> Log(LogRequests request, ServerCallContext context)
    { 
        var logClient = serviceScope.ServiceProvider.GetRequiredService<SendLog>();

        await logClient.Handle(request, context.CancellationToken);

        return new Empty();
    }
}