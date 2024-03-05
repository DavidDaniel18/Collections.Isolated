using Collections.Isolated.Domain.Dictionary.Synchronisation;
using Collections.Isolated.Domain.Dictionary.ValueObjects;

namespace Collections.Isolated.Application.Synchronisation;

internal sealed class SelectiveReleaseDistributedDecorator() : ISelectiveRelease
{
    public Task<bool> NextAcquireAsync(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }

    public Task ReleaseAsync(IntentionLock intentionLock)
    {
        throw new NotImplementedException();
    }
}