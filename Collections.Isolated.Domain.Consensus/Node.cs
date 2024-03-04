using Collections.Isolated.Domain.Common.Interfaces;
using Collections.Isolated.Domain.Common.Seedwork.Abstract;
using Collections.Isolated.Domain.Consensus.Events;
using Collections.Isolated.Domain.Consensus.State;
using Collections.Isolated.Domain.Consensus.ValueObjects;

namespace Collections.Isolated.Domain.Consensus;

internal sealed class Node : Aggregate<Node>
{
    private Proposal Proposal { get; set; }

    private int Term { get; set; }

    private string? _leaderId;

    private readonly IDatetimeProvider _datetimeProvider;

    private ConsensusState _consensusState;

    private Election _election;

    internal Node(string id, int clusterSize, IDatetimeProvider datetimeProvider, ILogging logging) : base(id, logging)
    {
        _datetimeProvider = datetimeProvider; 
        _consensusState = new Follower(this, datetimeProvider);
        _election = new Election(clusterSize, this);
        Proposal = new Proposal(0, id, id);
    }

    public void UpdateTimeout()
    {
        _consensusState.UpdateTimeout();
    }

    internal void FollowerNodeTimedOut()
    {
        RaiseDomainEvent(new FollowerTimedOut(Id));
    }

    public void BeginElection()
    {
        Term++;

        _consensusState = new Candidate(this, _datetimeProvider);

        VoteForCandidate(Proposal);
    }

    public int GetTerm()
    {
        return Term;
    }

    public void VoteForCandidate(Proposal otherNodeProposal)
    {
        var termComparison = CompareTerms(otherNodeProposal);

        if (termComparison == 0)
        {
            if (NodeAlreadyHadALeaderForThisTerm())
                return;

            _election.Vote(new Vote(otherNodeProposal.IssuerId, otherNodeProposal.NodeId));

            _election.TryEndElection();
        }
        else if(termComparison < 0)
        {
            throw new Exception("Candidate term is less than current term, it should have directly updated to ours");
        }
        else
        {
            ElectLeader(otherNodeProposal.NodeId);
        }

        bool NodeAlreadyHadALeaderForThisTerm()
        {
            return otherNodeProposal.NodeId.Equals(Id) is false;
        }
    }

    public Proposal OtherNodeRequestOurVote(Proposal otherNodeProposal)
    {
        var termComparison = CompareTerms(otherNodeProposal);

        if (termComparison == 0)
        {
            return new Proposal(Term, _leaderId ?? throw new Exception("LeaderId should not be null if term is not null"), Id);
        }
        else if (termComparison < 0)
        {
            return new Proposal(Term, _leaderId ?? throw new Exception("LeaderId should not be null if term is not null"), Id);
        }
        else
        {
            Term = otherNodeProposal.Term;

            ElectLeader(otherNodeProposal.NodeId);

            return new Proposal(Term, _leaderId ?? throw new Exception("Leader should not be null after being set"), Id);
        }
    }

    internal void SetState(ConsensusState consensusState)
    {
        _consensusState = consensusState;
    }

    internal void ElectLeader(string candidate)
    {
        _leaderId = candidate;

        // We are the leader
        if (candidate.Equals(Id))
        {
            RaiseDomainEvent(new PromotedToLeader(Id, Term));

            SetState(new Leader(this, _datetimeProvider));
        }
        else
        {
            RaiseDomainEvent(new AcceptedLeader(Id, candidate));

            SetState(new Follower(this, _datetimeProvider));
        }
    }

    private int CompareTerms(Proposal node)
    {
        return Compare(Term, node.Term);
    }

    // 0 = equal, 1 = better, -1 = worse
    private int Compare(int x, int toThis)
    {
        if (x > toThis)
        {
            return 1;
        }
        return x < toThis ? -1 : 0;
    }
}