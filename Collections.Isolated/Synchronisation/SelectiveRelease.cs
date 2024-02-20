using System.Collections.Concurrent;
using Collections.Isolated.Registration;

namespace Collections.Isolated.Synchronisation;

public class SelectiveRelease
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _waiters = new();
    private readonly ConcurrentQueue<string> _transactionLockIds = new();
    private volatile string _freeTransactionId = string.Empty;
    private int _firstOperation = 0;

    public async Task<bool> NextAcquireAsync(string transationId)
    {
        // Atomically set _freeId to id if it's currently empty
        var originalFreeId = Interlocked.CompareExchange(ref _freeTransactionId, transationId, string.Empty);

        // If this operation acquired the lock or if it's the first operation
        if (originalFreeId == string.Empty || _freeTransactionId.Equals(transationId))
        {
            var wasFirst = Interlocked.Exchange(ref _firstOperation, 1) == 0;

            return wasFirst;
        }

        _transactionLockIds.Enqueue(transationId);

        var tcs = new TaskCompletionSource<bool>();

        _waiters[transationId] = tcs;

        var resultTask = await Task.WhenAny(tcs.Task, Task.Delay(ServiceRegistration.TransactionTimeoutInMs));

        if (resultTask == tcs.Task)
        {
            return true;
        }

        _waiters.TryRemove(transationId, out _);

        throw new TimeoutException("The transaction lock acquisition timed out.");
    }

    public void Release()
    {
        if (_transactionLockIds.TryDequeue(out var nextFreeTransactionId))
        {
            Interlocked.Exchange(ref _freeTransactionId, nextFreeTransactionId);

            if (_waiters.TryRemove(nextFreeTransactionId, out var tcs))
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
            Interlocked.Exchange(ref _freeTransactionId, string.Empty);
        }
    }
}
