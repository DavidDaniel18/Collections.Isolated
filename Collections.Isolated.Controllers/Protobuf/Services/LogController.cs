using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Raft;

namespace Collections.Isolated.Controllers.Protobuf.Services;

public sealed class LogController : StateLogger.StateLoggerBase
{
    public override Task<Empty> Log(LogRequest request, ServerCallContext context)
    {
        
    }
}