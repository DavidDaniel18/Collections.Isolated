namespace Collections.Isolated.Domain.Common.Events.Interfaces;

public interface IHasDomainEvents
{
    IEnumerable<Event> DomainEvents { get; }

    void ClearDomainEvents();
}