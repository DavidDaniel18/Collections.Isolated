# Collections.Isolated

## Overview
This package introduces an approach to managing data transactions in memory for real-time systems. Instead of relying on a stable view of data throughout a transaction, this library ensures that transactions always interact with the most current data for specified keys. It is designed for scenarios where accessing the latest data is critical, making it ideal for real-time applications with minimal latency.

## Transaction Mechanism and Key-Specific Locking
The package employs a transaction mechanism that leverages an internal logging system and key-specific locking to ensure data integrity and immediacy. Here's a closer look at how it operates:

### Stating Intent and Acquiring Locks
1. **State Intent:** Transactions begin by stating their intent on specific keys, declaring whether they aim to read or write. This step is crucial for optimizing lock acquisition and minimizing contention by ensuring that locks are only applied where necessary.
   
2. **Lock Acquisition:** When a transaction states its intent on specific keys, it does not immediately lock them. Instead, locks are acquired dynamically as the transaction proceeds, allowing other operations to continue unimpeded on unrelated keys. Transactions on contented keys are done in FIFO.

### Catching Up to the Latest Data
Upon acquiring a lock on a key, the transaction catches up to the latest data for that key.

1. **Consulting the Internal Log:** Each time a lock is acquired, the transaction consults the internal log to identify any changes made to the key since the transaction was initiated. This log captures all modifications, ensuring no update is missed.

2. **Integrating Changes:** The transaction integrates these changes, updating its view of the data for the locked key to reflect the most current state. This ensures that any operation performed by the transaction is based on the latest data.

3. **Proceeding with the Transaction:** With an updated view, the transaction can then safely proceed, applying its changes or making decisions based on the most current data available.

## Usage
The package needs to be used with a dependency injection container. The package provides a `IServiceCollection` extension method to register the required services:

```csharp
using Collections.Isolated.Registration;

public void ConfigureServices(IServiceCollection services)
{
	services.AddIsolatedDictionary();
}
```

The package provides a `IDictionaryContext<>` interface that can be used to manage data transactions. Here's a basic example of how to use it:

```csharp
public class SessionManager(IDictionaryContext<SessionInfo> sessionDictionary, ISessionActivityLogger activityLogger)
{
    public async Task UpdateSessionActivity(string sessionId)
    {
        // optional, but greatly improves performance (the "false" is a readonly intent)
        sessionDictionary.StateIntent([sessionId], false);

        var sessionInfo = await sessionDictionary.TryGetAsync(sessionId) ?? new SessionInfo
        {
            SessionId = sessionId,
            LastActivity = DateTime.UtcNow,
            IsActive = true
        };

        sessionInfo.LastActivity = DateTime.UtcNow;
        sessionInfo.IsActive = true;

        await sessionDictionary.AddOrUpdateAsync(sessionId, sessionInfo);

        // External I/O operation that must be performed atomically with the session update
        await activityLogger.LogActivityAsync(sessionInfo);

        // Commit changes atomically, ensuring both in-memory and external operations are successful
        await sessionDictionary.SaveChangesAsync();
    }
}
```