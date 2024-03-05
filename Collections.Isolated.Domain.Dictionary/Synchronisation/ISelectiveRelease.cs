using Collections.Isolated.Domain.Dictionary.ValueObjects;

namespace Collections.Isolated.Domain.Dictionary.Synchronisation;

internal interface ISelectiveRelease
{
    Task<bool> NextAcquireAsync(IntentionLock intentionLock);
    Task ReleaseAsync(IntentionLock intentionLock);
}