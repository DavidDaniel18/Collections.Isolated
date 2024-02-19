using System.Collections.Concurrent;

namespace Collections.Isolated.Synchronisation;

public class SelectiveRelease<TValue> where TValue : class
{
    private readonly IsolationSync<TValue> _isolationSync;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _waiters = new();

    internal SelectiveRelease(IsolationSync<TValue> isolationSync)
    {
        _isolationSync = isolationSync;
    }

    public async Task WaitAsync(string id)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.None);

        _waiters[id] = tcs;

        using (var cancellationTokenSource = new CancellationTokenSource(1000)) // Set timeout to 1000ms
        {
            var delayTask = Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
            var completedTask = await Task.WhenAny(tcs.Task, delayTask);

            if (completedTask == delayTask)
            {
                // If the delay task completed first, remove the waiter and throw a TimeoutException
                _waiters.Remove(id, out _);

                //var transa =
                //    _isolationSync._dictionary._transactions.Values.Where(v => v._scheduler._currentAction is not null).ToList();
                throw new TimeoutException($"Operation timed out after 1000ms for id {id}.");
            }

            // Cancel the delay to clean up the cancellation token source
            await cancellationTokenSource.CancelAsync();
        }

        // Wait for the task completion source to be signaled
        await tcs.Task;
    }

    public void Release(string id)
    {
        TaskCompletionSource<bool>? tcs;
        if (_waiters.Remove(id, out tcs))
        {
            tcs.SetResult(true);
        }
    }
}