namespace Kumunita.Core.Media;

/// <summary>
/// Per-instance media store config (ADR 0011; OPS binds <c>Media__*</c>).
/// <see cref="RootPath"/> is the dedicated volume mount in production;
/// the dev default is <c>{baseDir}/media</c>. Raster-only allowlist
/// (C-MED·5); SVG is excluded.
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public string RootPath { get; set; } =
        System.IO.Path.Combine(AppContext.BaseDirectory, "media");

    /// <summary>Max payload bytes. 0 = unset (Web enforces the same constant).</summary>
    public long MaxBytes { get; set; } = 5L * 1024 * 1024; // 5 MiB

    /// <summary>Comma-separated allowed Content-Types (case-insensitive).</summary>
    public string? AllowedContentTypes { get; set; }

    public IEnumerable<string> ResolvedAllowedTypes =>
        (AllowedContentTypes ?? "image/jpeg,image/png,image/webp,image/gif")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

    public bool IsAllowed(string? contentType) =>
        !System.String.IsNullOrWhiteSpace(contentType)
        && ResolvedAllowedTypes.Any(t =>
            System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));
}
