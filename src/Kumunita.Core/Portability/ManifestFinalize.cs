namespace Kumunita.Core.Portability;

/// <summary>
/// The U03 manifest finalize (C-M11·1 / §manifest) — assembles the
/// <c>manifest.json</c> head from the U02 (docs) + U03 (media) outputs.
/// <para>
/// The field set is the <b>exact locked</b> §manifest shape —
/// <c>format</c> / <c>generated_at</c> / <c>community_name</c> /
/// <c>doc_counts</c> / <c>media_manifest</c> — no more, no less (the
/// U05 §validate checks (a)/(c)/(d) resolve against it; the U07 no-secret
/// pin byte-scans it).
/// </para>
/// </summary>
public static class ManifestFinalize
{
    /// <summary>
    /// Builds the finalized <c>manifest.json</c> POCO.
    /// </summary>
    /// <param name="communityName">
    /// The instance's display name (<c>CommunityOptions.Name</c>, §config).
    /// </param>
    /// <param name="docCounts">
    /// One <c>doc_counts</c> entry per §inventory content doc type — the
    /// U02 doc loop's output (the per-type row counts).
    /// </param>
    /// <param name="mediaManifest">
    /// One <c>media_manifest</c> entry per <c>MediaObject</c> in the
    /// catalog — the U03 media loop's output (the
    /// <c>{ id, size_bytes, content_type }</c> locked field set).
    /// </param>
    /// <param name="generatedAt">
    /// The export timestamp (a witness, not a decision).
    /// </param>
    /// <returns>The finalized <see cref="PortabilityManifest"/>.</returns>
    public static PortabilityManifest Build(
        string? communityName,
        Dictionary<string, int> docCounts,
        IReadOnlyList<PortabilityMediaEntry> mediaManifest,
        DateTimeOffset generatedAt)
    {
        return new PortabilityManifest
        {
            Format = PortabilityManifest.FormatVersion,
            GeneratedAt = generatedAt,
            CommunityName = communityName,
            DocCounts = docCounts,
            MediaManifest = new List<PortabilityMediaEntry>(mediaManifest),
        };
    }
}
