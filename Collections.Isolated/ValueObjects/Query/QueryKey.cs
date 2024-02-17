namespace Collections.Isolated.ValueObjects.Query;

internal sealed record QueryKey : ReadOperation
{
    internal QueryKey(string key)
    {
        Key = key;
    }

    internal string Key { get; set; }
}