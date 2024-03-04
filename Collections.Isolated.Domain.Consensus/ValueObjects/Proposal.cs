
namespace Collections.Isolated.Domain.Consensus.ValueObjects;

public record Proposal(int Term, string NodeId, string IssuerId);