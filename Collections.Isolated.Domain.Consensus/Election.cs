using System.Collections.Immutable;
using Collections.Isolated.Domain.Consensus.ValueObjects;

namespace Collections.Isolated.Domain.Consensus;

internal sealed class Election
{
    ImmutableHashSet<Vote> _votes = ImmutableHashSet<Vote>.Empty;

    internal readonly int ClusterSize;
    private readonly Node _node;

    public Election(int clusterSize, Node node)
    {
        ClusterSize = clusterSize;
        _node = node;
    }

    public void Vote(Vote vote)
    {
        _votes = _votes.Add(vote);
    }

    public bool TryEndElection()
    {
        if (_votes.Count > (ClusterSize / 2.0))
        {
            var voteGroups = _votes.GroupBy(vote => vote.Candidate)
                .OrderByDescending(group => group.Count());

            var mostVoted = voteGroups.First();

            var mostVoteCount = mostVoted.Count();

            _votes = ImmutableHashSet<Vote>.Empty;

            if (mostVoteCount <= (ClusterSize / 2.0))
            {
                return false;
            }

            _node.ElectLeader(mostVoted.Key);

            return true;
        }

        return false;
    }
}
