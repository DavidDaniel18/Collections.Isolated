using Collections.Isolated.Enums;

namespace Collections.Isolated.Entities;

/// <summary>
/// For Dependency Injection Interfaces
/// </summary>
public sealed record IntentionLock(string TransactionId, HashSet<string> KeysToLock, Intent Intent, TaskCompletionSource<bool> TaskCompletionSource);