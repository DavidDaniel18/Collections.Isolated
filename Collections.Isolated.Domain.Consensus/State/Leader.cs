using Collections.Isolated.Domain.Common.Interfaces;

namespace Collections.Isolated.Domain.Consensus.State;

internal sealed class Leader(Node node, IDatetimeProvider datetimeProvider) : ConsensusState(node, datetimeProvider)
{
    protected override void Promote()
    {
        throw new NotImplementedException();
    }

    protected override void Demote()
    {
        throw new NotImplementedException();
    }

    protected override int GetTimeout()
    {
        // don't timeout
        return int.MaxValue;
    }
}