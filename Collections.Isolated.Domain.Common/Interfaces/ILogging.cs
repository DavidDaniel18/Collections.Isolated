namespace Collections.Isolated.Domain.Common.Interfaces;

public interface ILogging
{
    public void LogInformation(string message);

    public void LogWarning(string message);

    public void LogError(string message);

    public void LogCritical(string message);

    public void LogDebug(string message);

    public void LogTrace(string message);
}