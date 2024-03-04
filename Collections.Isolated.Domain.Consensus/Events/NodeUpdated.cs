using Collections.Isolated.Domain.Common.Events;

namespace Collections.Isolated.Domain.Consensus.Events;

internal record NodeUpdated(string Id) : Event;