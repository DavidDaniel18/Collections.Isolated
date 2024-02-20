using System.Collections.Concurrent;

namespace Collections.Isolated.Synchronisation;

internal class PriorityFifoQueue<T>
{
    private readonly ConcurrentDictionary<IsolationScheduler.Priority, ConcurrentQueue<T>> _queues = new();

    public PriorityFifoQueue()
    {
        for (int i = 0; i < 3; i++)
        {
            _queues[(IsolationScheduler.Priority)i] = new ConcurrentQueue<T>();
        }
    }

    public void Enqueue(T item, IsolationScheduler.Priority priority)
    {
        _queues[priority].Enqueue(item);
    }

    public bool TryDequeue(out T? item, out IsolationScheduler.Priority priority)
    {
        item = default!;
        priority = default;

        for (int i = 0; i < 3; i++)
        {
            priority = (IsolationScheduler.Priority)i;

            if (_queues[priority].Count > 0)
            {
                return _queues[priority].TryDequeue(out item);
            }
        }

        return false;
    }
}