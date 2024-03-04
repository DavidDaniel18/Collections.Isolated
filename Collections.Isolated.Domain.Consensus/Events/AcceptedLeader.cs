using Collections.Isolated.Domain.Common.Events;

namespace Collections.Isolated.Domain.Consensus.Events;

internal record AcceptedLeader(string OurId, string LeaderId) : Event;
