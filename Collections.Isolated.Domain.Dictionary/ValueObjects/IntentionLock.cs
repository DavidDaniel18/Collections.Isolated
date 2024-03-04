using Collections.Isolated.Domain.Dictionary.Enums;

namespace Collections.Isolated.Domain.Dictionary.ValueObjects;

/// <summary>
/// For Dependency Injection Interfaces
/// </summary>
public sealed record IntentionLock(string TransactionId, HashSet<string> KeysToLock, Intent Intent, TaskCompletionSource<bool> TaskCompletionSource);