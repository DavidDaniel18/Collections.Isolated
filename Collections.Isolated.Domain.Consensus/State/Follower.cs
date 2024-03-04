using Collections.Isolated.Domain.Common.Interfaces;

namespace Collections.Isolated.Domain.Consensus.State;

internal sealed class Follower(Node node, IDatetimeProvider datetimeProvider) : ConsensusState(node, datetimeProvider)
{
    int Timeout = Random.Shared.Next(250, 350);

    protected override void Promote()
    {
        SetState(new Candidate(node, DatetimeProvider));
    }

    protected override void Demote()
    {
        node.FollowerNodeTimedOut();
    }

    protected override int GetTimeout()
    {
        return Timeout;
    }
}