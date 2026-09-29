using System.IO;
using System.Text;
using Kumunita.Core.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The 3 D6 file-sink pins (ADR 0114 D6; design doc §pinned tests). BCL-only,
/// no Testcontainers — <see cref="LogLine"/>, <see cref="RollingFileSink.BuildFileName"/>,
/// and <see cref="RollingFileSink.Retain"/> are pure over a temp dir (the
/// test cleans up in a <c>finally</c>). These pin the sink's output shape
/// (the C-M13·1 "local feedback" boundary — one JSON-decodable line per
/// entry, the <c>exception</c> field omitted when null) and the rotation +
/// retention contracts (<see cref="F5"/> "the sink is boring").
/// </summary>
public class LoggingTests
{
    private static string NewTempDir()
    {
        var dir = System.IO.Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static void SafeDeleteDir(string dir)
    {
        try { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
        catch { /* best-effort cleanup */ }
    }

    /// <summary>
    /// The D6 "omitted when null" + valid-JSON pin: a <c>LogLine.Write</c>
    /// over a <see cref="StringWriter"/> produces one JSON-decodable line with
    /// the <c>timestamp</c> / <c>level</c> / <c>category</c> / <c>message</c>
    /// fields; the <c>exception</c> field is present when the <see cref="Exception"/>
    /// is non-null and absent when it is null.
    /// </summary>
    [Fact]
    public void RollingFileSink_LogLine_Is_ValidJsonLines()
    {
        var ts = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

        // With an exception: all five fields, exception present.
        var withEx = new StringBuilder();
        var w1 = new StringWriter(withEx);
        LogLine.Write(w1, LogLevel.Error, "Kumunita.Test", "boom \"here\"", new InvalidOperationException("oh no"), ts);
        var line1 = withEx.ToString().Trim();
        var o1 = System.Text.Json.JsonDocument.Parse(line1).RootElement;
        Assert.Equal("2026-09-28T12:00:00.0000000+00:00", o1.GetProperty("timestamp").GetString());
        Assert.Equal("error", o1.GetProperty("level").GetString());
        Assert.Equal("Kumunita.Test", o1.GetProperty("category").GetString());
        Assert.Equal("boom \"here\"", o1.GetProperty("message").GetString());
        Assert.True(o1.TryGetProperty("exception", out var exProp), "exception field must be present when the Exception is non-null");
        Assert.Contains("oh no", exProp.GetString());

        // Without an exception: exception field omitted.
        var withoutEx = new StringBuilder();
        var w2 = new StringWriter(withoutEx);
        LogLine.Write(w2, LogLevel.Information, "Kumunita.Test", "hello", null, ts);
        var line2 = withoutEx.ToString().Trim();
        var o2 = System.Text.Json.JsonDocument.Parse(line2).RootElement;
        Assert.Equal("2026-09-28T12:00:00.0000000+00:00", o2.GetProperty("timestamp").GetString());
        Assert.Equal("information", o2.GetProperty("level").GetString());
        Assert.Equal("Kumunita.Test", o2.GetProperty("category").GetString());
        Assert.Equal("hello", o2.GetProperty("message").GetString());
        Assert.False(o2.TryGetProperty("exception", out _), "exception field must be omitted when the Exception is null");
    }

    /// <summary>
    /// The D6 rotation pin: two <see cref="DateTimeOffset"/>s on different days
    /// produce two distinct <c>app-yyyyMMdd.log</c> names; the same day produces
    /// the same name.
    /// </summary>
    [Fact]
    public void RollingFileSink_FileNaming_Is_Daily()
    {
        var dir = NewTempDir();
        try
        {
            var d1 = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
            var d2 = new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
            var d1b = new DateTimeOffset(2026, 9, 27, 23, 59, 59, TimeSpan.Zero);

            var name1 = RollingFileSink.BuildFileName(d1, dir);
            var name2 = RollingFileSink.BuildFileName(d2, dir);
            var name1b = RollingFileSink.BuildFileName(d1b, dir);

            Assert.NotEqual(name1, name2);
            Assert.Equal(name1, name1b);
            Assert.EndsWith("app-20260927.log", name1);
            Assert.EndsWith("app-20260928.log", name2);
        }
        finally
        {
            SafeDeleteDir(dir);
        }
    }

    /// <summary>
    /// The D6 retention pin: plant three <c>app-*.log</c> files with distinct
    /// mtimes (the oldest older than the cutoff, the other two within it);
    /// <c>Retain(dir, days: 2, now)</c> deletes the oldest and keeps the other two.
    /// </summary>
    [Fact]
    public void RollingFileSink_Retention_Deletes_Older_Files()
    {
        var dir = NewTempDir();
        try
        {
            var now = new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
            var retentionDays = 2;
            var cutoff = now.AddDays(-retentionDays); // 2026-09-26T00:00:00Z

            // Oldest: 5 days old (older than the cutoff) → deleted.
            var old = System.IO.Path.Combine(dir, "app-20260923.log");
            File.WriteAllText(old, "old");
            File.SetLastWriteTimeUtc(old, now.AddDays(-5).UtcDateTime);

            // Recent 1: 1 day old (within the cutoff) → kept.
            var recent1 = System.IO.Path.Combine(dir, "app-20260927.log");
            File.WriteAllText(recent1, "recent1");
            File.SetLastWriteTimeUtc(recent1, now.AddDays(-1).UtcDateTime);

            // Recent 2: today (within the cutoff) → kept.
            var recent2 = System.IO.Path.Combine(dir, "app-20260928.log");
            File.WriteAllText(recent2, "recent2");
            File.SetLastWriteTimeUtc(recent2, now.UtcDateTime);

            var deleted = RollingFileSink.Retain(dir, retentionDays, now);

            Assert.Equal(1, deleted);
            Assert.False(File.Exists(old), "the oldest file must be deleted");
            Assert.True(File.Exists(recent1), "the 1-day-old file must be kept");
            Assert.True(File.Exists(recent2), "the today file must be kept");
        }
        finally
        {
            SafeDeleteDir(dir);
        }
    }
}
