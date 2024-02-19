using System.Diagnostics;

namespace Collections.Isolated.Synchronisation;

public class IsolationScheduler
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly PriorityFifoQueue<Action> _taskQueue = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly SemaphoreSlim _semaphore = new(0);
    private Action? _currentAction;
    private readonly Task _processingTask;
    private bool _canAdd = true;
    private readonly Stopwatch _stopwatch = new();

    public IsolationScheduler(CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _processingTask = Task.Factory.StartNew(ProcessQueueAsync, TaskCreationOptions.LongRunning);

        _processingTask.ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Console.WriteLine(task.Exception);
            }
        });
    }

    public void Schedule(Action task, Priority priority)
    {
        if (_cancellationTokenSource.IsCancellationRequested || _canAdd is false) return;

        try
        {
            EnterWriteLock();

            _taskQueue.Enqueue(task, priority);

            _semaphore.Release();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void ProcessQueueAsync()
    {
        while (true)
        {
            try
            {
                _semaphore.Wait();

                if (_cancellationTokenSource.IsCancellationRequested) return;

                try
                {
                    EnterWriteLock();
                    
                    if (_taskQueue.TryDequeue(out _currentAction, out _) is false)
                    {
                        continue;
                    }

                    _currentAction.Invoke();

                    _currentAction = null;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            catch (Exception e) when (e is TaskCanceledException)
            {
                throw;
            }
        }
    }

    public void WaitForCompletionAsync()
    {

        _stopwatch.Start();

        try
        {
            EnterWriteLock();

            _canAdd = false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        
        while (true)
        {
            if (_stopwatch.ElapsedMilliseconds > 1_000)
            {
                throw new TimeoutException("Could not cancel transaction processing scheduler");
            }

            try
            {
                EnterReadLock();

                if (_currentAction == null && 
                    (_taskQueue.TryDequeue(out _, out var priority) is false || priority is Priority.Medium or Priority.High))
                {
                    _cancellationTokenSource.Cancel(false);

                    _semaphore.Release();

                    break;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public enum Priority
    {
        Low = 2,
        Medium = 1,
        High = 0
    }

    private void EnterReadLock()
    {
        if (_lock.TryEnterReadLock(100) is false) throw new TimeoutException("Could not acquire read lock");
    }

    private void EnterWriteLock()
    {
        if (_lock.TryEnterWriteLock(100) is false) throw new TimeoutException("Could not acquire read lock");
    }
}