using Collections.Isolated.Domain.Common.Interfaces;

namespace Collections.Isolated.Domain.Consensus.State;

internal sealed class Candidate(Node node, IDatetimeProvider datetimeProvider) : ConsensusState(node, datetimeProvider)
{
    protected override void Promote()
    {
        SetState(new Leader(node, datetimeProvider));
    }

    protected override void Demote()
    {
        SetState(new Follower(node, datetimeProvider));
    }

    protected override int GetTimeout()
    {
        // don't timeout
        return int.MaxValue;
    }
}