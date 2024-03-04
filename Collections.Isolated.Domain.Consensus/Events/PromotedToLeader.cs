using Collections.Isolated.Domain.Common.Events;

namespace Collections.Isolated.Domain.Consensus.Events;

internal record PromotedToLeader(string Id, int Term) : Event;