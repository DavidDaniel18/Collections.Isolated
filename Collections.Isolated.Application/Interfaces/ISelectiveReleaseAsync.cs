using Collections.Isolated.Domain.Dictionary.ValueObjects;

namespace Collections.Isolated.Application.Interfaces;

public interface ISelectiveReleaseAsync<TValue> : ISelectiveReleaseAsync
{
   
}

public interface ISelectiveReleaseAsync
{
    Task<bool> NextAcquire(IntentionLock intentionLock);

    Task ReleaseAsync(IntentionLock intentionLock);
}