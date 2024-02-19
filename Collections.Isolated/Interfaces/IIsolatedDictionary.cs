namespace Collections.Isolated.Interfaces;

public interface IIsolatedDictionary<TValue> where TValue : class
{
    Task<TValue?> GetAsync(string key, string transactionId);
    Task AddOrUpdateAsync(string key, TValue value, string transactionId);
    Task RemoveAsync(string key, string transactionId);
    Task BatchApplyOperationAsync(IEnumerable<(string key, TValue value)> items, string transactionId);
    Task SaveChangesAsync(string transactionId);
    Task<int> CountAsync(string transactionId);
    void UndoChanges(string transactionId);
}