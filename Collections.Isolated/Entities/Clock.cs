namespace Collections.Isolated.Entities;

internal static class Clock
{
    private static long _counter = 0;

    internal static long GetTicks()
    {
        return Interlocked.Increment(ref _counter);
    }
}