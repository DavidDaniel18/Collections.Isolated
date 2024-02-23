using Collections.Isolated.Enums;

namespace Collections.Isolated.Entities;

public sealed record IntentionLock(string TransactionId, string[] KeysToLock, Intent Intent, TaskCompletionSource<bool> TaskCompletionSource);