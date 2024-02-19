namespace Collections.Isolated.ValueObjects.Query;

internal abstract record ReadOperation(long CreationTime) : Operation(CreationTime);