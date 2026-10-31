namespace Kumunita.Core.Portability;

/// <summary>
/// Zip-bomb guard limits for <see cref="KumunitaArchive.ReadAsync"/>
/// (SEC-lane U13 / M2). Applied while streaming the archive: the
/// entry count is checked before any bytes are read; the per-entry
/// decompressed size and total decompressed size are checked as bytes
/// are read. Any violation throws <see cref="System.IO.InvalidDataException"/>
/// before further bytes are consumed.
/// </summary>
/// <param name="MaxEntries">Maximum number of entries allowed in the archive.</param>
/// <param name="MaxEntryBytes">Maximum decompressed size of a single entry, in bytes.</param>
/// <param name="MaxTotalBytes">Maximum total decompressed size of all entries, in bytes.</param>
public sealed record ArchiveLimits(
    int MaxEntries,
    long MaxEntryBytes,
    long MaxTotalBytes)
{
    /// <summary>
    /// Safe defaults pinned in the design doc §2.2:
    /// 10 000 entries · 100 MB per entry · 500 MB total.
    /// </summary>
    public static ArchiveLimits Default { get; } =
        new(10_000, 100L * 1024 * 1024, 500L * 1024 * 1024);
}
