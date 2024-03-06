using Collections.Isolated.Domain.Dictionary.Enums;
using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Consistency;

namespace Collections.Isolated.Application.Converters.Interfaces;

internal sealed class IntentionLockMapper : IMappingTo<RpcIntentionLock, IntentionLock>
{
    public IntentionLock MapFrom(RpcIntentionLock dto)
    {
        var transactionId = dto.TransactionId;
        var keysToLock = new HashSet<string>(dto.KeysToLock);
        var intent = (Intent)dto.Intent;
        var taskCompletionSource = new TaskCompletionSource<bool>();

        return new IntentionLock(transactionId, keysToLock, intent, taskCompletionSource);
    }
}