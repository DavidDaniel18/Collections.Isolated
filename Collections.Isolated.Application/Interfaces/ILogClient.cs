using Raft;

namespace Collections.Isolated.Application.Interfaces;

internal interface ILogClient
{
    Task Log(LogRequests logs, CancellationToken cancellationToken);
}