namespace Collections.Isolated.ValueObjects;

public abstract class Operation<TKey, TValue> 
    where TValue : class 
    where TKey : notnull
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}