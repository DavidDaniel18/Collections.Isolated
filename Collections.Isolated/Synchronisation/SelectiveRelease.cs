using System.Collections.Concurrent;
using Collections.Isolated.Registration;

namespace Collections.Isolated.Synchronisation;

public class SelectiveRelease
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _waiters = new();
    private readonly ConcurrentQueue<string> _transactionLockIds = new();
    private volatile string _freeId = string.Empty;
    private int _firstOperation = 0;

    public async Task<bool> NextAcquireAsync(string id)
    {
        // Atomically set _freeId to id if it's currently empty
        var originalFreeId = Interlocked.CompareExchange(ref _freeId, id, string.Empty);

        // If this operation acquired the lock or if it's the first operation
        if (originalFreeId == string.Empty || _freeId.Equals(id))
        {
            var wasFirst = Interlocked.Exchange(ref _firstOperation, 1) == 0;

            return wasFirst;
        }

        _transactionLockIds.Enqueue(id);

        var tcs = new TaskCompletionSource<bool>();

        _waiters[id] = tcs;

        var resultTask = await Task.WhenAny(tcs.Task, Task.Delay(ServiceRegistration.TransactionTimeoutInMs));

        if (resultTask == tcs.Task)
        {
            return true;
        }

        _waiters.TryRemove(id, out _);

        throw new TimeoutException("The transaction lock acquisition timed out.");
    }

    public void Release()
    {
        if (_transactionLockIds.TryDequeue(out var nextId))
        {
            Interlocked.Exchange(ref _freeId, nextId);

            if (_waiters.TryRemove(nextId, out var tcs))
            {
                tcs.SetResult(true);
            }
            else
            {
                // This should never happen
                throw new InvalidOperationException("The transaction lock id was not found in the waiters collection.");
            }
        }
        else
        {
            // Reset for the next operation, ensure atomicity and visibility
            Interlocked.Exchange(ref _firstOperation, 0);
            Interlocked.Exchange(ref _freeId, string.Empty);
        }
    }
}
