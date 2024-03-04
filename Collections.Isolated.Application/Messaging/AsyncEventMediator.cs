using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Common.Events;
using Collections.Isolated.Domain.Common.Interfaces;

namespace Collections.Isolated.Application.Messaging;

internal class AsyncEventMediator : IPublisher, IConsumer
{
    private readonly ILogging _logger;

    private static readonly ConcurrentDictionary<Delegate, CancellationTokenSource> Observers = new();

    private static readonly ConcurrentDictionary<Type, ImmutableList<Channel<object>>> Channels = new();

    internal AsyncEventMediator(ILogging logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Subscribes to an event of type TEvent and processes it through the given funnels, returning a result of type
    ///     TResult.
    /// </summary>
    public async void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> asyncEventHandler) where TEvent : Event
    {
        var channel = Channel.CreateUnbounded<object>();

        var cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = cancellationTokenSource.Token;

        try
        {
            if (Observers.TryAdd(asyncEventHandler, cancellationTokenSource))
            {
                SafeAddChannelOfType<TEvent>(channel, cancellationToken);

                await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    var result = message as TEvent ?? throw new InvalidOperationException($"Expected type {typeof(TEvent)} but got {message.GetType()}");

                    await asyncEventHandler.Invoke(result, cancellationToken).ConfigureAwait(false);
                }

                throw new ChannelClosedException($"Channel of type {typeof(TEvent)} has been closed");
            }

            throw new InvalidOperationException("This event handler has already been subscribed");
        }
        catch (OperationCanceledException)
        {
            // exit gracefully
        }
        catch (Exception e)
        {
            _logger?.LogError("Error in event handler");
            throw;
        }
        finally
        {
            SafeRemoveChannelOfType<TEvent>(channel);
        }
    }

    public void UnSubscribe<TEvent>(Func<TEvent, CancellationToken, Task> asyncEventHandler) where TEvent : Event
    {
        try
        {
            if (Observers.TryRemove(asyncEventHandler, out var cancellationTokenSource))
                cancellationTokenSource.Cancel();
            else
                throw new InvalidOperationException("This event handler has not been subscribed");
        }
        catch (Exception)
        {
            _logger?.LogError("Error Unsubscribing from event");
            throw;
        }
    }

    public void TryUnSubscribe<TEvent>(Func<TEvent, CancellationToken, Task> asyncEventHandler) where TEvent : Event
    {
        if (Observers.TryRemove(asyncEventHandler, out var cancellationTokenSource))
            cancellationTokenSource.Cancel();
    }

    public async Task Publish<TEvent>(TEvent message) where TEvent : Event
    {
        try
        {
            var type = typeof(TEvent);

            if (Channels.TryGetValue(type, out var channels))
            {
                foreach (var channel in channels)
                    await channel.Writer.WriteAsync(message);
            }
        }
        catch (Exception)
        {
            _logger?.LogError("Error publishing event");
            throw;
        }
        
    }

    private void SafeAddChannelOfType<TEvent>(Channel<object> channel, CancellationToken cancellationToken) where TEvent : class
    {
        var type = typeof(TEvent);

        Channels.AddOrUpdate(type, _ =>
        {
            var channels = ImmutableList<Channel<object>>.Empty.Add(channel);

            return channels;
        }, (_, channels) => channels.Add(channel));
    }

    private void SafeRemoveChannelOfType<TEvent>(Channel<object> channel) where TEvent : class
    {
        var type = typeof(TEvent);

        Channels.AddOrUpdate(type, _ => ImmutableList<Channel<object>>.Empty, (_, channels) => channels.Remove(channel));
    }
}