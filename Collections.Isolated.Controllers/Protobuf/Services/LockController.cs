using Consistency;
using Grpc.Core;

namespace Collections.Isolated.Controllers.Protobuf.Services;

public sealed class LockController : DistributedLock.DistributedLockBase
{
    public override Task<Result> NextAcquireAsync(RpcIntentionLock request, ServerCallContext context)
    {
    }

    public override Task<Result> ReleaseAsync(RpcIntentionLock request, ServerCallContext context)
    {
    }
}