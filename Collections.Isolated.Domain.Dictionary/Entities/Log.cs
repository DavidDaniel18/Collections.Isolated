using Collections.Isolated.Domain.Dictionary.Interfaces;
using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Domain.Dictionary.Entities;

internal sealed class Log<TValue> : ILog<TValue>
{
    private volatile IReadOnlyDictionary<string, WriteOperation> _log = new Dictionary<string, WriteOperation>();

    private long _lastLogTime = -1;

    public IReadOnlyDictionary<string, WriteOperation> GetLog() => _log;

    public long GetLastLogTime() => Interlocked.Read(ref _lastLogTime);

    public void UpdateLog(IReadOnlyDictionary<string, WriteOperation> log)
    {
        Interlocked.Exchange(ref _log, log);
        Interlocked.Exchange(ref _lastLogTime, Clock.GetTicks());
    }
}