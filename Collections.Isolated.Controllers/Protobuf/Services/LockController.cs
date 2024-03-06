using Collections.Isolated.Application.Converters.Interfaces;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Consistency;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Type = System.Type;

namespace Collections.Isolated.Controllers.Protobuf.Services;

public sealed class LockController(IServiceProvider serviceProvider) : DistributedLock.DistributedLockBase
{
    private static readonly IMappingTo<RpcIntentionLock, IntentionLock> IntentionLockMapper = new IntentionLockMapper();

    public override Task<Result> NextAcquire(RpcIntentionLock request, ServerCallContext context)
    {
        return base.NextAcquire(request, context);
    }

    public override Task<Empty> Release(RpcIntentionLock request, ServerCallContext context)
    {
        var intentionLock = IntentionLockMapper.MapFrom(request);

        var syncStore = GetSelectiveReleaseForType(request);

        return base.Release(request, context);
    }

    public override async Task<Empty> EnsureTransactionCreated(RpcIntentionLock request, ServerCallContext context)
    {
        var intentionLock = IntentionLockMapper.MapFrom(request);

        var syncStore = GetSyncStoreForType(request);

        await syncStore.EnsureTransactionCreatedAsync(intentionLock);

        return new Empty();
    }

    private ISyncStoreAsync GetSyncStoreForType(RpcIntentionLock request)
    {
        var tvalue = Type.GetType(request.TypeName) ?? throw new InvalidOperationException("Type not found");

        var syncStoreGenericType = typeof(ISyncStoreAsync<>).MakeGenericType(tvalue);

        var syncStore = serviceProvider.GetService(syncStoreGenericType) as ISyncStoreAsync ?? throw new InvalidOperationException("Service not found");
        
        return syncStore;
    }

    private ISelectiveReleaseAsync GetSelectiveReleaseForType(RpcIntentionLock request)
    {
        var tvalue = Type.GetType(request.TypeName) ?? throw new InvalidOperationException("Type not found");

        var selectiveReleaseGenericType = typeof(ISelectiveReleaseAsync<>).MakeGenericType(tvalue);

        var selectiveRelease = serviceProvider.GetService(selectiveReleaseGenericType) as ISelectiveReleaseAsync ?? throw new InvalidOperationException("Service not found");

        return selectiveRelease;
    }
}