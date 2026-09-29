// FileLogger.cs — the ILogger impl (BeginScope / Log / IsEnabled; the
// Log method's LogLine.Write call, the TextWriter per-category lock,
// the day-rollover: re-open the writer on a new day's file).
using System.IO;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Logging;

public sealed class FileLogger : ILogger
{
    private readonly object     _lock   = new();
    private TextWriter          _writer;
    private readonly string     _dir;
    private readonly string     _category;
    private string              _openFile; // the file path _writer is currently open on

    public FileLogger(string category, string dir)
    {
        _category = category;
        _dir      = dir;
        Directory.CreateDirectory(dir);
        _openFile = RollingFileSink.BuildFileName(DateTimeOffset.UtcNow, dir);
        _writer   = new StreamWriter(_openFile) { AutoFlush = true };
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <summary>A no-op scope (the file sink carries no structured scope state — the <see cref="Log{TState}"/> formatter already receives the state).</summary>
    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        lock (_lock)
        {
            var today = RollingFileSink.BuildFileName(DateTimeOffset.UtcNow, _dir);
            if (today != _openFile)
            {
                // Day-rollover: close the old writer, open the new day's file.
                _writer.Dispose();
                _openFile = today;
                _writer   = new StreamWriter(today) { AutoFlush = true };
            }
            LogLine.Write(_writer, logLevel, _category, formatter(state, exception), exception, DateTimeOffset.UtcNow);
        }
    }

    public void Dispose()
    {
        lock (_lock) _writer.Dispose();
    }
}
