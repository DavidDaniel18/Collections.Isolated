
namespace Collections.Isolated.Domain.Dictionary.ValueObjects.Query;

internal sealed record QueryKey(string Key, long CreationTime) : ReadOperation(CreationTime);