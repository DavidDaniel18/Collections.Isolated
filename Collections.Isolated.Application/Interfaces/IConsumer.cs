using Collections.Isolated.Domain.Common.Events;

namespace Collections.Isolated.Application.Interfaces;

public interface IConsumer
{
    void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> asyncEventHandler) where TEvent : Event;

    void UnSubscribe<TEvent>(Func<TEvent, CancellationToken, Task> asyncEventHandler) where TEvent : Event;

    void TryUnSubscribe<TEvent>(Func<TEvent, CancellationToken, Task> asyncEventHandler) where TEvent : Event;
}