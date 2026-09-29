namespace Kumunita.Core.Logging;

/// <summary>
/// The M13 file-sink options (ADR 0114 D6). The
/// <c>CommunityOptions</c> / <c>MediaOptions</c> bind shape — a POCO
/// with a <see cref="SectionName"/> constant, bound in
/// <c>Program.cs</c> by the <c>FileSinkOptions</c> read
/// (<c>Logging__File__Directory</c> /
/// <c>Logging__File__RetentionDays</c>).
/// </summary>
public sealed class FileSinkOptions
{
    /// <summary>Configuration section name (bound by the host, e.g. <c>Logging__File__Directory</c>).</summary>
    public const string SectionName = "Logging__File";

    /// <summary>The directory the dated <c>app-*.log</c> files land in (created if absent, the <see cref="RollingFileSink"/> housekeeping).</summary>
    public string Directory { get; set; } = "logs";

    /// <summary>The retention day-count (files older than this, by mtime, are deleted at boot — the <see cref="RollingFileSink.Retain"/> pass).</summary>
    public int RetentionDays { get; set; } = 14;
}
