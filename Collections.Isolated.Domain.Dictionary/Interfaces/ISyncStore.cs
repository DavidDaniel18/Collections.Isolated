using Collections.Isolated.Domain.Dictionary.ValueObjects;
using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Domain.Dictionary.Interfaces;

internal interface ISyncStore<TValue>
{
    void SaveChanges(string transactionId);
    int Count();
    TValue? Get(string key, string transactionId);
    void AddOrUpdate(string key, TValue value, string transactionId);
    void Remove(string key, string transactionId);
    void UndoChanges(string transactionId);
    void EnsureTransactionCreated(IntentionLock transactionLock);
    bool ContainsTransaction(string transactionId);
    void UpdateTransactionWithLog(string transactionId, IEnumerable<WriteOperation> log, long lastLogTime);
    List<TValue> GetTrackedEntities(string intentionLockTransactionId);
    List<TValue> GetAll(string intentionLockTransactionId);
}