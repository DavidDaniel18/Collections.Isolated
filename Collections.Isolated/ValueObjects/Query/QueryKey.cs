
namespace Collections.Isolated.ValueObjects.Query;

internal sealed record QueryKey(string Key, long CreationTime) : ReadOperation(CreationTime);