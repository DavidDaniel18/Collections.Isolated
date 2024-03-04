namespace Collections.Isolated.Domain.Dictionary.ValueObjects.Query;

internal abstract record ReadOperation(long CreationTime) : Operation(CreationTime);