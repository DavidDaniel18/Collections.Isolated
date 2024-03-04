using Collections.Isolated.Domain.Dictionary.Enums;
using Collections.Isolated.Domain.Dictionary.Interfaces;
using Collections.Isolated.Domain.Dictionary.ValueObjects;

namespace Collections.Isolated.Domain.Dictionary.Synchronisation;

internal class SelectiveRelease(ITransactionSettings transactionSettings)
{
    // Key: transactionId
    private readonly List<IntentionLock> _waitingTransactions = new();

    private readonly HashSet<string> _onGoingTransactions = new();
    // Key: Key, Value: Intent
    private readonly Dictionary<string, Intent> _intentLocks = new();

    private readonly ReaderWriterLockSlim _ongoingLock = new(LockRecursionPolicy.SupportsRecursion);

    private Intent _fullLockIntent = Intent.None;

    internal async Task<bool> NextAcquireAsync(IntentionLock intentionLock)
    {
        try
        {
            _ongoingLock.EnterUpgradeableReadLock();

            if (_onGoingTransactions.Contains(intentionLock.TransactionId))
            {
                return false;
            }

            try
            {
                _ongoingLock.EnterWriteLock();

                _waitingTransactions.Add(intentionLock);

                TryAcquireLock(intentionLock);
            }
            finally
            {
                _ongoingLock.ExitWriteLock();
            }
        }
        finally
        {
            _ongoingLock.ExitUpgradeableReadLock();
        }

        var resultTask = await Task.WhenAny(intentionLock.TaskCompletionSource.Task, Task.Delay(transactionSettings.GetTransactionTimeoutInMs()));

        if (resultTask == intentionLock.TaskCompletionSource.Task)
        {
            return true;
        }

        throw new TimeoutException("The transaction lock acquisition timed out.");
    }

    internal void Release(IntentionLock intentionLock)
    {
        try
        {
            _ongoingLock.EnterWriteLock();

            _onGoingTransactions.Remove(intentionLock.TransactionId);

            IfFullLockIntentIsWriteDemoteToNone();

            foreach (var key in intentionLock.KeysToLock)
            {
                _intentLocks.Remove(key);
            }

            TryAcquireLocks();
        }
        finally
        {
            _ongoingLock.ExitWriteLock();
        }
    }

    private void TryAcquireLocks()
    {
        for (int i = 0; i < _waitingTransactions.Count; i++)
        {
            TryAcquireLock(_waitingTransactions[i]);
        }
    }

    private void IfFullLockIntentIsWriteDemoteToNone()
    {
        if (_fullLockIntent is Intent.Write)
            _fullLockIntent = Intent.None;
    }

    private void TryAcquireLock(IntentionLock currentIntent)
    {
        if (currentIntent.TaskCompletionSource.Task.IsCompleted) return;

        if (currentIntent.KeysToLock.Count == 0)
        {
            if (_fullLockIntent is Intent.Write) return;

            if (currentIntent.Intent == Intent.Read)
            {
                _fullLockIntent = Intent.Read;

                PromoteTaskToLock(currentIntent);
            }
            else if (NoCurrentLocks())
            {
                _fullLockIntent = Intent.Write;

                PromoteTaskToLock(currentIntent);
            }

            return;
        }

        HashSet<string> lockedKeys = new();

        int availableLocks = 0;

        foreach (var key in currentIntent.KeysToLock)
        {
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
        if (availableLocks == currentIntent.KeysToLock.Count)
        {
            // Set the locks and release the task
            foreach (var key in currentIntent.KeysToLock)
            {
                _intentLocks[key] = currentIntent.Intent;

            }

            PromoteTaskToLock(currentIntent);
        }
    }

    private bool NoCurrentLocks()
    {
        return _intentLocks.Count == 0;
    }

    private void PromoteTaskToLock(IntentionLock currentIntent)
    {
        _waitingTransactions.Remove(currentIntent);
        _onGoingTransactions.Add(currentIntent.TransactionId);

        currentIntent.TaskCompletionSource.SetResult(true);
    }

    public void Forfeit(IntentionLock intentionLock)
    {
        try
        {
            _ongoingLock.EnterWriteLock();

            if (_onGoingTransactions.Contains(intentionLock.TransactionId))
            {
                Release(intentionLock);
            }
            else
            {
                _waitingTransactions.Remove(intentionLock);
            }
        }
        finally
        {
            _ongoingLock.ExitWriteLock();

        }
    }
}
