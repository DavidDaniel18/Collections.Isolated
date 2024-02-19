namespace Collections.Isolated.Interfaces;

public interface IIsolatedDictionary<TValue> where TValue : class
{
    Task<TValue?> GetAsync(string key, string transactionId);
    void AddOrUpdate(string key, TValue value, string transactionId);
    void Remove(string key, string transactionId);
    void BatchApplyOperation(IEnumerable<(string key, TValue value)> items, string transactionId);
    Task SaveChangesAsync(string transactionId);
    Task<int> CountAsync(string transactionId);
    void UndoChanges(string transactionId);
}