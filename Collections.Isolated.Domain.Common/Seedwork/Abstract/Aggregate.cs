using Collections.Isolated.Domain.Common.Events;
using Collections.Isolated.Domain.Common.Events.Interfaces;
using Collections.Isolated.Domain.Common.Interfaces;

namespace Collections.Isolated.Domain.Common.Seedwork.Abstract;

public abstract class Aggregate<T> : Entity<T>, IHasDomainEvents where T : class
{
    protected readonly ILogging Logging;
    private readonly List<Event> _domainEvents = new();

    protected Aggregate(string id, ILogging logging) : base(id)
    {
        Logging = logging;
    }

    public IEnumerable<Event> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    protected void RaiseDomainEvent(Event eventItem)
    {
        _domainEvents.Add(eventItem);
    }
}