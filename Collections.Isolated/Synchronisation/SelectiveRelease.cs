using System.Collections.Concurrent;

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