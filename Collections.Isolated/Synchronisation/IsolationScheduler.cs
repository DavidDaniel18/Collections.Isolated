using Collections.Isolated.Registration;
using System.Diagnostics;

namespace Collections.Isolated.Synchronisation;

public class IsolationScheduler
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly PriorityFifoQueue<Action> _taskQueue = new();
    private readonly SemaphoreSlim _semaphore = new(0);
    private Action? _currentAction;
    private volatile bool _canAdd = true;
    private readonly Task _task;
     
    public IsolationScheduler()
    {
        _task = ProcessQueueAsync();
    }

    public void Schedule(Action task, Priority priority)
    {
        if (_cancellationTokenSource.IsCancellationRequested || _canAdd is false) return;

        _taskQueue.Enqueue(task, priority);

        _semaphore.Release();
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            await _semaphore.WaitAsync();

            if (_cancellationTokenSource.IsCancellationRequested) return;

            if (_taskQueue.TryDequeue(out _currentAction, out _) is false || _currentAction is null)
            {
                throw new InvalidOperationException("The task queue is empty.");
            }

            var cts = new CancellationTokenSource(1_000);

            await Task.Run(_currentAction, cts.Token);

            _currentAction = null;
        }
    }

    public async Task WaitForCompletionAsync()
    {
        _canAdd = false;

        var cts = new CancellationTokenSource(1_000);

        await Task.Run(Wait, cts.Token);

        return;

        Task Wait()
        {
            while (true)
            {
                if (_task.IsCompleted || _task.IsCanceled || _task.IsFaulted)
                {
                    throw _task.Exception ?? new AggregateException("The task was cancelled.");
                }

                if (_currentAction == null &&
                    (_taskQueue.TryDequeue(out _, out var priority) is false || priority is Priority.Medium or Priority.High))
                {
                    _cancellationTokenSource.Cancel(false);

                    Interlocked.MemoryBarrier();

                    _semaphore.Release();

                    break;
                }
            }

            return Task.CompletedTask;
        }
    }

    public enum Priority
    {
        Low = 2,
        Medium = 1,
        High = 0
    }
}