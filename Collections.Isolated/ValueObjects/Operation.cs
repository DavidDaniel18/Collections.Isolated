namespace Collections.Isolated.ValueObjects;

internal abstract record Operation(DateTime DateTime)
{
    internal Operation() : this(DateTime.UtcNow) { }
}