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

    /// <summary>
    /// Comma-separated allowed Content-Types for the attachment lane (case-insensitive).
    /// Distinct from <see cref="AllowedContentTypes"/> (the image lane) — the attachment
    /// lane has its own gate (C-ATT·6); the config key is
    /// <c>Media:AttachmentAllowedContentTypes</c>. Positive-only; SVG excluded.
    /// </summary>
    public string? AttachmentAllowedContentTypes { get; set; }

    /// <summary>
    /// The resolved attachment allowlist (C-ATT·6). Positive-only; SVG excluded;
    /// the raster image types are included so a resident can attach a photo *as a
    /// download* without also using the Image button. Reuses <see cref="MaxBytes"/>
    /// (not a second size cap). The image lane's <see cref="ResolvedAllowedTypes"/>
    /// is untouched (C-ATT·9).
    /// </summary>
    public IEnumerable<string> ResolvedAttachmentAllowedTypes =>
        (AttachmentAllowedContentTypes ??
         "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/plain,text/csv,application/zip,image/jpeg,image/png,image/webp,image/gif")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

    /// <summary>
    /// Whether <paramref name="contentType"/> is on the attachment allowlist
    /// (case-insensitive; C-ATT·6). Mirrors <see cref="IsAllowed"/> over the
    /// attachment set — the image lane's <see cref="IsAllowed"/> is untouched.
    /// </summary>
    public bool IsAttachmentAllowed(string? contentType) =>
        !System.String.IsNullOrWhiteSpace(contentType)
        && ResolvedAttachmentAllowedTypes.Any(t =>
            System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));
}
