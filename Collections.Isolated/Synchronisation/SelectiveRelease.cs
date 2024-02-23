using Collections.Isolated.Entities;
using Collections.Isolated.Enums;
using Collections.Isolated.Registration;

namespace Collections.Isolated.Synchronisation;

internal class SelectiveRelease
{
    // Key: transactionId
    private readonly List<IntentionLock> _waitingTransactions = new();

    private readonly HashSet<string> _onGoingTransactions = new();
    // Key: Key, Value: Intent
    private readonly Dictionary<string, Intent> _intentLocks = new();

    private SpinLock _spinLock;

    private readonly ReaderWriterLockSlim _ongoingLock = new ();

    private Intent _fullLockIntent;

    internal async Task<bool> NextAcquireAsync(IntentionLock intentionLock)
    {
        bool lockTaken = false;

        var tasks = new List<TaskCompletionSource<bool>>();

        try
        {
            _ongoingLock.EnterReadLock();

            if (_onGoingTransactions.Contains(intentionLock.TransactionId))
            {
                return false;
            }
        }
        finally
        {
            _ongoingLock.ExitReadLock();
        }

        try
        {
            _spinLock.Enter(ref lockTaken);

            _waitingTransactions.Add(intentionLock);

            tasks.AddRange(TryAcquireLocks());
        }
        catch (Exception ex)
        {
            throw new Exception("An error occurred while attempting to acquire the transaction lock.", ex);
        }
        finally
        {
            if (lockTaken)
            {
                _spinLock.Exit();
            }

            UnlockTransactions(tasks);
        }

        var resultTask = await Task.WhenAny(intentionLock.TaskCompletionSource.Task, Task.Delay(ServiceRegistration.TransactionTimeoutInMs));

        if (resultTask == intentionLock.TaskCompletionSource.Task)
        {
            return true;
        }

        throw new TimeoutException("The transaction lock acquisition timed out.");
    }

    internal void Release(IntentionLock intentionLock)
    {
        bool lockTaken = false;

        var tasks = new List<TaskCompletionSource<bool>>();

        try
        {
            _ongoingLock.EnterWriteLock();

            _onGoingTransactions.Remove(intentionLock.TransactionId);
        }
        finally
        {
            _ongoingLock.ExitWriteLock();
        }

        try
        {
            _spinLock.Enter(ref lockTaken);

            IfFullLockIntentIsWriteDemoteToNone();

            _onGoingTransactions.Remove(intentionLock.TransactionId);

            for (var i = 0; i < intentionLock.KeysToLock.Length; i++)
            {
                _intentLocks.Remove(intentionLock.KeysToLock[i]);
            }

            if(_waitingTransactions.Count>0)
                tasks = TryAcquireLocks();
        }
        finally
        {
            if (lockTaken)
            {
                _spinLock.Exit();
            }

            UnlockTransactions(tasks);
        }
    }

    private static void UnlockTransactions(List<TaskCompletionSource<bool>> tasks)
    {
        for (int i = 0; i < tasks.Count; i++)
        {
            tasks[i].SetResult(true);
        }
    }

    private void IfFullLockIntentIsWriteDemoteToNone()
    {
        if (_fullLockIntent is Intent.Write)
            _fullLockIntent = Intent.None;
    }

    private List<TaskCompletionSource<bool>> TryAcquireLocks()
    {
        var tasks = new List<TaskCompletionSource<bool>>();

        for (int i = 0; i < _waitingTransactions.Count; i++)
        {
            var completionSource = TryAcquireLock(_waitingTransactions[i]);

            if (completionSource is not null)
            {
                tasks.Add(completionSource);
            }
        }

        return tasks;
    }

    private TaskCompletionSource<bool>? TryAcquireLock(IntentionLock currentIntent)
    {
        if (_fullLockIntent is Intent.Write) return null;

        HashSet<string> lockedKeys = new();

        int availableLocks = 0;

        if (currentIntent.KeysToLock.Length == 0)
        {
            if (currentIntent.Intent == Intent.Read)
            {
                _fullLockIntent = Intent.Read;

                return PromoteTaskToLock(currentIntent);
            }
            if (NoCurrentLocks())
            {
                _fullLockIntent = Intent.Write;

                return PromoteTaskToLock(currentIntent);
            }

            return null;
        }

        for (int i = 0; i < currentIntent.KeysToLock.Length; i++)
        {
            var key = currentIntent.KeysToLock[i];

            // If the key is already locked, skip it
            if (lockedKeys.Contains(key)) continue;

            // If the key is locked, check if it's locked for writing
            if (_intentLocks.TryGetValue(key, out var intent))
            {
                // nothing can be done if the key is locked for writing, add to the locked keys
                if (intent == Intent.Write)
                {
                    lockedKeys.Add(key);
                }
                // if the key is locked for reading, it can be read again
                else
                {
                    if (currentIntent.Intent == Intent.Read)
                    {
                        lockedKeys.Add(key);
                        availableLocks++;
                    }
                    // if the key is locked for reading but the current intent is to write, block further reading
                    else if (currentIntent.Intent == Intent.Write)
                    {
                        lockedKeys.Add(key);
                    }
                }
            }
            // not contained in locked keys
            else
            {
                lockedKeys.Add(key);
                availableLocks++;
            }
        }

        // If all locks are available
        if (availableLocks == currentIntent.KeysToLock.Length)
        {
            // Set the locks and release the task
            for (var i = 0; i < currentIntent.KeysToLock.Length; i++)
            {
                _intentLocks[currentIntent.KeysToLock[i]] = currentIntent.Intent;
            }

            return PromoteTaskToLock(currentIntent);
        }

        return null;
    }

    private bool NoCurrentLocks()
    {
        return _intentLocks.Count == 0;
    }

    private TaskCompletionSource<bool> PromoteTaskToLock(IntentionLock currentIntent)
    {
        _waitingTransactions.Remove(currentIntent);
        _onGoingTransactions.Add(currentIntent.TransactionId);

        currentIntent.TaskCompletionSource.SetResult(true);
        return currentIntent.TaskCompletionSource;
    }
}
