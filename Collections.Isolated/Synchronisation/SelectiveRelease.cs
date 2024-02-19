using System.Collections.Concurrent;

namespace Collections.Isolated.Synchronisation;

public class SelectiveRelease
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _waiters = new();
    private readonly ConcurrentQueue<string> _transactionLockIds = new();
    private string _freeId = string.Empty;

    public async Task WaitAsync(string id)
    {
        Interlocked.CompareExchange(ref _freeId, id, string.Empty);

        if (_freeId.Equals(id)) return;

        _transactionLockIds.Enqueue(id);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.None);

        _waiters[id] = tcs;

        using (var cancellationTokenSource = new CancellationTokenSource(10_000)) // Set timeout to 1000ms
        {
            var delayTask = Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
            var completedTask = await Task.WhenAny(tcs.Task, delayTask);

            if (completedTask == delayTask)
            {
                // If the delay task completed first, remove the waiter and throw a TimeoutException
                _waiters.Remove(id, out _);
                throw new TimeoutException($"Operation timed out after 1000ms for id {id}.");
            }

            // Cancel the delay to clean up the cancellation token source
            await cancellationTokenSource.CancelAsync();
        }

        // Wait for the task completion source to be signaled
        await tcs.Task;
    }

    public void Release()
    {
        TaskCompletionSource<bool>? tcs;

        if (_transactionLockIds.Count > 0 && _transactionLockIds.TryDequeue(out _freeId) && _waiters.TryRemove(_freeId, out tcs))
        {
            tcs.SetResult(true);
        }
        else
        {
            _freeId = string.Empty;
        }
    }
}