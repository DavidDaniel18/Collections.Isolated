using Collections.Isolated.Application.Commands.Election;
using Collections.Isolated.Application.Commands.Logs;
using Collections.Isolated.Application.Commands.Updates;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Consensus.Events;
using Raft;

namespace Collections.Isolated.Controllers.EventHandlers;

internal sealed class NodeEventHandler(SendLog sendLog, UpdateNode updateNode, BeginNewElection beginNewElection, IConsumer consumer) : IDisposable
{
    internal void Register()
    {
        consumer.Subscribe<PromotedToLeader>(SendLogJob);
        consumer.Subscribe<NodeUpdated>(UpdateNodeAfterDelay);
        consumer.Subscribe<FollowerTimedOut>(BeginNewElectionAndUpdateNode);
        consumer.Subscribe<AcceptedLeader>(StopSendingLogsAsLeader);
    }

    public void Dispose()
    {
        consumer.TryUnSubscribe<PromotedToLeader>(SendLogJob);
        consumer.TryUnSubscribe<NodeUpdated>(UpdateNodeAfterDelay);
        consumer.TryUnSubscribe<FollowerTimedOut>(BeginNewElectionAndUpdateNode);
        consumer.TryUnSubscribe<AcceptedLeader>(StopSendingLogsAsLeader);
    }

    private async Task SendLogJob(PromotedToLeader message, CancellationToken token)
    {
        while (token.IsCancellationRequested is false)
        {
            await sendLog.Handle(new LogRequests() { SenderId = message.Id, Term = message.Term, }, token);

            await Task.Delay(100, token).ConfigureAwait(false);
        }
    }

    private async Task UpdateNodeAfterDelay(NodeUpdated message, CancellationToken token)
    {
        await Task.Delay(50, token).ConfigureAwait(false);

        await updateNode.Handle(message.Id);
    }


    private async Task BeginNewElectionAndUpdateNode(FollowerTimedOut message, CancellationToken token)
    {
        await beginNewElection.Handle(message.Id, token);

        await updateNode.Handle(message.Id);
    }

    private Task StopSendingLogsAsLeader(AcceptedLeader acceptedLeader, CancellationToken cancellationToken)
    {
        consumer.TryUnSubscribe<PromotedToLeader>(SendLogJob);
        consumer.Subscribe<PromotedToLeader>(SendLogJob);

        return Task.CompletedTask;
    }
}