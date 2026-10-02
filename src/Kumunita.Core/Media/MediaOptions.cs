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

    /// <summary>
    /// Comma-separated allowed Content-Types for the document lane (case-insensitive;
    /// M21, ADR 0122 D3). Distinct from <see cref="AttachmentAllowedContentTypes"/>
    /// (C-ATT·6) and <see cref="AllowedContentTypes"/> (C-MED·5) — the document lane
    /// has its own gate; the config key is
    /// <c>Media:DocumentAllowedContentTypes</c>. Positive-only; SVG excluded (a
    /// document is a download, never inline — D6).
    /// </summary>
    public string? DocumentAllowedContentTypes { get; set; }

    /// <summary>
    /// The resolved document allowlist (M21, ADR 0122 D3). A neighborhood set of
    /// OFFICIAL document types — PDF / Office docs / text / csv / zip (the
    /// attachment set minus the raster image types — an official document is not
    /// a photo). Reuses <see cref="MaxBytes"/> (not a second size cap). The
    /// attachment lane's <see cref="ResolvedAttachmentAllowedTypes"/> and the
    /// image lane's <see cref="ResolvedAllowedTypes"/> are both untouched.
    /// </summary>
    public IEnumerable<string> ResolvedDocumentAllowedTypes =>
        (DocumentAllowedContentTypes ??
         "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/plain,text/csv,application/zip")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

    /// <summary>
    /// Whether <paramref name="contentType"/> is on the document allowlist
    /// (case-insensitive; M21, ADR 0122 D3). Mirrors <see cref="IsAttachmentAllowed"/>
    /// over the document set — the attachment lane's
    /// <see cref="IsAttachmentAllowed"/> and the image lane's
    /// <see cref="IsAllowed"/> are untouched.
    /// </summary>
    public bool IsDocumentAllowed(string? contentType) =>
        !System.String.IsNullOrWhiteSpace(contentType)
        && ResolvedDocumentAllowedTypes.Any(t =>
            System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Comma-separated allowed Content-Types for the **inline-preview** subset
    /// of the attachment lane (case-insensitive; ADR 0126 C-PV·2). The
    /// browser-displayable members of <see cref="AttachmentAllowedContentTypes"/>
    /// (C-PV·10) — the four raster image types, <c>application/pdf</c>,
    /// <c>text/plain</c>, <c>text/csv</c>. A previewable type is always also an
    /// attachment-allowed type (C-PV·10); deliberately a **separate** member
    /// (not derived from the allowlist) so growing the upload allowlist never
    /// silently grows the inline-render surface. Positive-only; SVG /
    /// <c>text/html</c> / <c>image/svg+xml</c> / <c>application/javascript</c>
    /// are never previewable (C-PV·1). The config key is
    /// <c>Media:PreviewableContentTypes</c>.
    /// </summary>
    public string? PreviewableContentTypes { get; set; }

    /// <summary>
    /// The resolved previewable set (ADR 0126 C-PV·2). The browser-displayable
    /// members of the attachment allowlist (the default), overridable by
    /// <see cref="PreviewableContentTypes"/>. A subset of
    /// <see cref="ResolvedAttachmentAllowedTypes"/> (C-PV·10).
    /// </summary>
    public IEnumerable<string> ResolvedPreviewableTypes =>
        (PreviewableContentTypes ??
         "image/jpeg,image/png,image/webp,image/gif,application/pdf,text/plain,text/csv")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

    /// <summary>
    /// Whether <paramref name="contentType"/> is a **previewable** attachment
    /// (ADR 0126 C-PV·2): case-insensitive membership in
    /// <see cref="ResolvedPreviewableTypes"/>. Mirrors
    /// <see cref="IsAttachmentAllowed"/> / <see cref="IsDocumentAllowed"/>.
    /// The **only** classification the serve branch (C-PV·3) consults.
    /// </summary>
    public bool IsPreviewable(string? contentType) =>
        !System.String.IsNullOrWhiteSpace(contentType)
        && ResolvedPreviewableTypes.Any(t =>
            System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));
}
