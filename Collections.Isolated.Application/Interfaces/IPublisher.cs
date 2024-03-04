using Collections.Isolated.Domain.Common.Events;

namespace Collections.Isolated.Application.Interfaces;

public interface IPublisher
{
    Task Publish<TEvent>(TEvent message) where TEvent : Event;
}