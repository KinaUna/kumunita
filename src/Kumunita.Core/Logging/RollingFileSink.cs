namespace Kumunita.Core.Logging;

/// <summary>
/// The pure daily-rotation + retention surface (ADR 0114 D6). BCL-only
/// (a temp dir via <c>Path.GetTempPath()</c> +
/// <c>Path.GetRandomFileName()</c> for the pins — the test cleans up in
/// a <c>finally</c>; the <c>LogLine</c> / <c>BuildFileName</c> /
/// <c>Retain</c> are pure over a temp dir, **no Testcontainers**).
/// </summary>
public static class RollingFileSink
{
    /// <summary>
    /// The daily file name under <paramref name="dir"/> for
    /// <paramref name="now"/> — the <c>app-yyyyMMdd.log</c> naming
    /// (the D6 rotation pin: two <c>DateTimeOffset</c>s on different
    /// days produce two distinct names).
    /// </summary>
    public static string BuildFileName(DateTimeOffset now, string dir)
    {
        ArgumentNullException.ThrowIfNull(dir);
        return System.IO.Path.Combine(dir, $"app-{now:yyyyMMdd}.log");
    }

    /// <summary>
    /// The retention pass (the D6 "delete at boot" convention — the
    /// <c>AuditPurgeService</c> "delete + no summary" shape, minus the
    /// summary: the file deletion is the sink's own housekeeping, not a
    /// domain write). Enumerates <c>app-*.log</c> in
    /// <paramref name="dir"/>; deletes any whose
    /// <c>File.GetLastWriteTimeUtc</c> is older than
    /// <paramref name="now"/> − <paramref name="retentionDays"/>; the
    /// <c>File.Delete</c> is per-file in a <c>try/catch</c> (a file
    /// being written at the instant of the pass degrades to a skipped
    /// file, not a crash — the <c>AuditPurgeService</c> "no per-row
    /// crash" shape).
    /// </summary>
    /// <returns>The count of files deleted.</returns>
    public static int Retain(string dir, int retentionDays, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(dir);
        if (!System.IO.Directory.Exists(dir)) return 0;

        var cutoff = now.AddDays(-retentionDays);
        var deleted = 0;
        foreach (var f in System.IO.Directory.EnumerateFiles(dir, "app-*.log"))
        {
            try
            {
                if (System.IO.File.GetLastWriteTimeUtc(f) < cutoff)
                {
                    System.IO.File.Delete(f);
                    deleted++;
                }
            }
            catch (System.IO.IOException)
            {
                // A file being written at the instant of the pass: skip
                // it, don't crash (the D6 "no per-row crash" shape).
            }
        }
        return deleted;
    }
}
