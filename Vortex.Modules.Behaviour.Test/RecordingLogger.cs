using Microsoft.Extensions.Logging;

namespace Vortex.Modules.Behaviour.Test;

/// <summary>
/// Keeps every line that was logged, so a test can read back what the runner
/// said it was doing.
/// </summary>
internal class RecordingLogger<T>(LogLevel minimumLevel = LogLevel.Debug) : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= minimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        Lines.Add(formatter(state, exception));
    }
}
