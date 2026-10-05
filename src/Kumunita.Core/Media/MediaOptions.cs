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
    public long MaxBytes { get; set; } = 10L * 1024 * 1024; // 10 MiB

    /// <summary>
    /// Optional **platform-wide** storage-space limit in bytes (env knob
    /// <c>Media__MaxPlatformBytes</c>). When set to a value &gt; 0, the platform
    /// may use at most this many bytes of resident content: the <c>/admin/storage</c>
    /// "available" figure is capped to this budget, and all new uploads are
    /// blocked (413) once used space reaches it **or** the volume's physical free
    /// space drops below the 100 MiB floor. <c>0</c> or unset = **unlimited**
    /// (the default — the admin "available" figure then reports the physical
    /// free space, exactly as before). This is an operator knob (OPS.md), not an
    /// admin-set in-app value.
    /// </summary>
    public long MaxPlatformBytes { get; set; } = 0; // 0 = unlimited

    /// <summary>
    /// The physical free-space floor (bytes) below which new uploads are blocked
    /// regardless of the platform limit — the 100 MiB operator safety margin that
    /// keeps the volume from filling to its physical edge. A constant, not a knob.
    /// </summary>
    public const long MinFreeSpaceFloor = 100L * 1024 * 1024; // 100 MiB

    /// <summary>Comma-separated allowed Content-Types (case-insensitive).</summary>
    public string? AllowedContentTypes { get; set; }

    public IEnumerable<string> ResolvedAllowedTypes =>
        (AllowedContentTypes ?? "image/jpeg,image/png,image/webp,image/gif")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

    public bool IsAllowed(string? contentType) =>
        AllowedTypeMatches(ResolvedAllowedTypes, contentType);

    public bool IsAttachmentAllowed(string? contentType) =>
        AllowedTypeMatches(ResolvedAttachmentAllowedTypes, contentType);

    public bool IsDocumentAllowed(string? contentType) =>
        AllowedTypeMatches(ResolvedDocumentAllowedTypes, contentType);

    public bool IsPreviewable(string? contentType) =>
        AllowedTypeMatches(ResolvedPreviewableTypes, contentType);

    /// <summary>
    /// The single allowlist membership test every <c>Is*</c> method funnels
    /// through. Case-insensitive and **parameter-insensitive**: it compares only
    /// the MIME type (the part before any <c>;</c>) so a client's
    /// <c>text/plain; charset=utf-8</c>, <c>text/csv; charset=utf-8</c>, or
    /// <c>application/pdf; name=…</c> matches the plain <c>text/plain</c> /
    /// <c>text/csv</c> / <c>application/pdf</c> entry. Browsers append
    /// <c>charset</c> (and occasionally other) parameters to the Content-Type
    /// they post, and a prior exact-match implementation rejected those with a
    /// 415 even for allowlisted types (the same class of bug as
    /// <c>application/x-zip-compressed</c> vs. <c>application/zip</c>).
    /// A malicious base type is still refused — <c>text/html; x</c> fails
    /// because its base <c>text/html</c> is not allowlisted — so this does not
    /// widen the closed set.
    /// </summary>
    private static bool AllowedTypeMatches(System.Collections.Generic.IEnumerable<string> allowlist, string? contentType) =>
        !System.String.IsNullOrWhiteSpace(contentType)
        && allowlist.Any(t => System.String.Equals(t, BaseMime(contentType), System.StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The client-supplied MIME type with any <c>;</c>-delimited parameters
    /// stripped and surrounding whitespace trimmed — the base type to compare
    /// against a bare allowlist entry (<c>"text/plain; charset=utf-8"</c> →
    /// <c>"text/plain"</c>).
    /// </summary>
    private static string BaseMime(string contentType)
    {
        var semicolon = contentType.IndexOf(';');
        var baseType = semicolon >= 0 ? contentType.Substring(0, semicolon) : contentType;
        return baseType.Trim();
    }

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
         "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/plain,text/csv,application/zip,application/x-zip-compressed,audio/mpeg,audio/mp3,audio/mp4,video/mp4,image/jpeg,image/png,image/webp,image/gif")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

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
         "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/plain,text/csv,application/zip,application/x-zip-compressed")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

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

}
