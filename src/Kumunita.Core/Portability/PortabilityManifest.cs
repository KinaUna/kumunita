namespace Kumunita.Core.Portability;

/// <summary>
/// The <c>manifest.json</c> POCO set — the archive's self-describing head
/// (M11 D2 / C-M11·1; the exact locked field set from
/// <c>docs/design/m11-portability-design.md</c> §manifest, copied
/// verbatim by U03's manifest finalize and asserted by U07's pins).
/// <para>
/// The <see cref="Format"/> field is the <em>authority</em> — the
/// <c>*.kumunita</c> extension is a content marker, not a format claim
/// (C-M11·1). Import rejects any archive whose <c>format</c> is not one
/// the build understands (fail-closed, no partial apply).
/// </para>
/// </summary>
public sealed class PortabilityManifest
{
    /// <summary>
    /// The closed M11 format version string — the frozen pin
    /// (<c>kumunita/portability/1</c>). A different / missing / malformed
    /// value on import is a <c>format.unsupported</c> rejection (§validate
    /// check (a)).
    /// </summary>
    public const string FormatVersion = "kumunita/portability/1";

    /// <summary>The format version — the authority (C-M11·1).</summary>
    public string Format { get; set; } = FormatVersion;

    /// <summary>The export timestamp (a witness, not a decision).</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>
    /// The instance's display name (<c>CommunityOptions.Name</c>, §config) —
    /// the archive's self-description.
    /// </summary>
    public string? CommunityName { get; set; }

    /// <summary>
    /// One entry per §inventory content doc type — the U02 doc loop's
    /// count (the per-type isolation that keeps the round-trip testable).
    /// </summary>
    public Dictionary<string, int> DocCounts { get; set; } = new();

    /// <summary>
    /// One entry per <c>MediaObject</c> in the catalog — the U03 media
    /// loop's output (the §validate (d) byte-verification target set).
    /// </summary>
    public List<PortabilityMediaEntry> MediaManifest { get; set; } = new();

    /// <summary>
    /// The D2 resident-scope marker. <c>"resident"</c> = a M27
    /// resident-scoped archive; <c>null</c> = a plain M11 whole-instance
    /// archive (the ADR 0098 additive-frozen-surface precedent: existing
    /// readers keep compiling, a plain M11 archive still imports whole).
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>The subjectId of the resident this archive is scoped to (D2, D3).</summary>
    public string? ResidentSubjectId { get; set; }
}

/// <summary>
/// One <c>media_manifest</c> entry (the exact locked field set
/// <c>{ id, size_bytes, content_type }</c> — §manifest; U03's media loop
/// writes it from the <c>MediaObject</c> catalog, U05's validator copies
/// it verbatim).
/// </summary>
public sealed class PortabilityMediaEntry
{
    /// <summary>The content hash — the lowercase-hex SHA-256 of the payload.</summary>
    public string Id { get; set; } = "";

    /// <summary>The payload size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>The validated content type (e.g. "image/png").</summary>
    public string ContentType { get; set; } = "";
}
