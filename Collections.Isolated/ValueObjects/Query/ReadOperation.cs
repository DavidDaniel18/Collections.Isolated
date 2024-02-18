namespace Collections.Isolated.ValueObjects.Query;

internal abstract record ReadOperation(DateTime DateTime) : Operation(DateTime);