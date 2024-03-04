using Collections.Isolated.Domain.Common.Events;

namespace Collections.Isolated.Domain.Consensus.Events;

internal record FollowerTimedOut(string Id) : Event;