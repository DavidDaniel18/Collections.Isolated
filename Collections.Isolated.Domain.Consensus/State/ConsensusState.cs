using Collections.Isolated.Domain.Common.Interfaces;

namespace Collections.Isolated.Domain.Consensus.State;

internal abstract class ConsensusState
{
    private Node _node;

    protected readonly IDatetimeProvider DatetimeProvider;

    protected DateTime LastHeartbeat { get; set; }

    protected ConsensusState(Node node, IDatetimeProvider datetimeProvider)
    {
        _node = node;
        DatetimeProvider = datetimeProvider;
    }

    protected abstract void Promote();

    protected abstract void Demote();

    protected abstract int GetTimeout();

    internal void SetState(ConsensusState consensusState)
    {
        _node.SetState(consensusState);
    }

    internal void UpdateTimeout()
    {
        CheckTimeout();
        PreventTimeout();
    }

    private void PreventTimeout()
    {
        LastHeartbeat = DatetimeProvider.GetCurrentTime();
    }

    private void CheckTimeout()
    {
        var timedOut = DatetimeProvider.GetCurrentTime() > LastHeartbeat.AddMilliseconds(GetTimeout());

        if (timedOut)
        {
            Demote();
        }
    }
}