namespace Collections.Isolated.Synchronisation;

internal class PriorityFifoQueue<T>
{
    private readonly Dictionary<IsolationScheduler.Priority, Queue<T>> _queues = new();

    public PriorityFifoQueue()
    {
        for (int i = 0; i < 3; i++)
        {
            _queues[(IsolationScheduler.Priority)i] = new Queue<T>();
        }
    }

    public void Enqueue(T item, IsolationScheduler.Priority priority)
    {
        _queues[priority].Enqueue(item);
    }

    public bool TryDequeue(out T item, out IsolationScheduler.Priority priority)
    {
        for (int i = 0; i < 3; i++)
        {
            priority = (IsolationScheduler.Priority)i;

            if (_queues[priority].Count > 0)
            {
                item = _queues[priority].Dequeue();

                return true;
            }
        }

        item = default;
        priority = default;
        return false;
    }

    public int Count()
    {
        int count = 0;
        foreach (var queue in _queues.Values)
        {
            count += queue.Count;
        }
        return count;
    }
}