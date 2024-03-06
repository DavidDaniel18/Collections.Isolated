using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Dictionary.Interfaces;
using Collections.Isolated.Domain.Dictionary.Synchronisation;
using Collections.Isolated.Domain.Dictionary.ValueObjects;

namespace Collections.Isolated.Application.Decorators.SelectiveRelease;

internal sealed class DistributedSelectiveReleaseDecorator<TValue>(ITransactionSettings transactionSettings) : ISelectiveReleaseAsync<TValue>
{
    public Task<bool> NextAcquire(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task ReleaseAsync(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }
}