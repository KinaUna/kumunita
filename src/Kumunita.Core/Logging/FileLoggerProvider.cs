// FileLoggerProvider.cs — the ILoggerProvider impl (CreateLogger returns
// a FileLogger; Dispose closes all open loggers; the one-FileLogger-per-
// category shape, the TextWriter per-category lock).
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly int    _retentionDays;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string dir, int retentionDays)
    {
        _dir = dir;
        _retentionDays = retentionDays;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, n => new FileLogger(n, _dir));

    public void Dispose()
    {
        foreach (var l in _loggers.Values) l.Dispose();
        _loggers.Clear();
    }
}
