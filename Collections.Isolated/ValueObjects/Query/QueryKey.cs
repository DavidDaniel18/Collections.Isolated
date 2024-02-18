
namespace Collections.Isolated.ValueObjects.Query;

internal sealed record QueryKey(string Key, DateTime DateTime) : ReadOperation(DateTime);