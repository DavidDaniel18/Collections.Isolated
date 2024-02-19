﻿namespace Collections.Isolated.Synchronisation;

public class IsolationScheduler
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly PriorityQueue<Action, int> _taskQueue = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly SemaphoreSlim _semaphore = new(0);
    internal Action? _currentAction;
    private Task _processingTask;
    private bool _isProcessing = true;
    private bool _canAdd = true;

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
            _lock.EnterWriteLock();

            _taskQueue.Enqueue(task, (int)priority);

            _semaphore.Release();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void ProcessQueueAsync()
    {
        while (_isProcessing)
        {
            try
            {
                _semaphore.Wait();

                if (_cancellationTokenSource.IsCancellationRequested) return;

                try
                {
                    _lock.EnterWriteLock();

                    if (_taskQueue.TryDequeue(out _currentAction, out _) is false)
                    {
                        continue;
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                _currentAction.Invoke();

                _currentAction = null;

            }
            catch (Exception e) when (e is TaskCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    public void WaitForCompletionAsync()
    {
        while (true)
        {
            try
            {
                _lock.EnterReadLock();

                if (_currentAction == null && (_taskQueue.TryPeek(out _, out var priority) is false || priority > 0))
                {
                    return;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public void BlockAdditions()
    {
        _canAdd = false;
    }

    public void StopProcessing()
    {
        _isProcessing = false;

        _semaphore.Release();
    }

    public enum Priority
    {
        Low = 2,
        Medium = 1,
        High = 0
    }
}
