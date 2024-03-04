using Collections.Isolated.Domain.Common.Seedwork.Interfaces;

namespace Collections.Isolated.Domain.Consensus.ValueObjects;

public sealed record Vote(string Issuer, string Candidate) : IValueObject<Vote>;
