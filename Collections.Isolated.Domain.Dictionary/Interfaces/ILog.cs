using Collections.Isolated.Domain.Dictionary.ValueObjects.Commands;

namespace Collections.Isolated.Domain.Dictionary.Interfaces;

internal interface ILog<TValue>
{
    IReadOnlyDictionary<string, WriteOperation> GetLog();
    long GetLastLogTime();
    void UpdateLog(IReadOnlyDictionary<string, WriteOperation> log);
}