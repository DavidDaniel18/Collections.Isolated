using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Dictionary.Interfaces;
using Collections.Isolated.Domain.Dictionary.Synchronisation;
using Collections.Isolated.Domain.Dictionary.ValueObjects;

namespace Collections.Isolated.Application.Adaptors;

internal sealed class ApplicationSelectiveReleaseAdaptor<TValue>(ITransactionSettings transactionSettings) : ISelectiveReleaseAsync<TValue>
{
    private readonly SelectiveRelease<TValue> _selectiveRelease = new(transactionSettings);

    public Task<bool> NextAcquire(IntentionLock intentionLock)
    {
        return _selectiveRelease.NextAcquire(intentionLock);
    }

    public Task ReleaseAsync(IntentionLock intentionLock)
    {
        _selectiveRelease.Release(intentionLock);

        return Task.CompletedTask;
    }
}