namespace Kumunita.Core.SurfaceLabels;

/// <summary>
/// The read + write seams for the <c>SurfaceLabels</c> singleton (ADR 0152,
/// M29). The <c>GetLabelAsync</c> read is the shared label-resolver the nav +
/// header views call (M29·1) — best-effort (a missing row / read failure
/// degrades to the <c>kw-l</c> fallback, never throws, never blank),
/// world-readable, never audited (M29·2, ADR 0001-B thin-token). The
/// <c>SaveAsync</c> write is the ADR 0150 single audited write-lane shape
/// (M29·5): one session, the doc + exactly one <c>AccessAudit</c> row commit
/// together (invariant C3), strong consistency (the new value is live on the
/// very next read / render, invariant C4). The lane **upserts** the singleton
/// — it never creates a second row (M29·6); a missing row is a no-op.
/// </summary>
public interface ISurfaceLabelsService
{
    /// <summary>
    /// The shared label-resolver (M29·1 — the nav + header call **one** helper
    /// so they resolve to the **same** value). Returns the admin-set label
    /// **if present and non-blank**, else the <c>kw-l</c> translation of
    /// <paramref name="fallbackKey"/> in <paramref name="effectiveLanguage"/>
    /// (the "empty = use default" shape, M29·3 / M29·4). **Best-effort**: a
    /// missing row (a fresh boot before the seeder ran), a missing store, or a
    /// read failure degrades to the <c>kw-l</c> fallback — never throws, never
    /// blank (M29·2). **World-readable**, never an access decision, never a
    /// claim (ADR 0001-B thin-token), never audited (M29·2).
    /// </summary>
    /// <param name="surfaceKey">
    /// The surface key (one of the 13: <c>home</c>, <c>announcements</c>,
    /// <c>community</c>, <c>groups</c>, <c>events</c>, <c>projects</c>,
    /// <c>inventory</c>, <c>bookmarks</c>, <c>documents</c>, <c>pages</c>,
    /// <c>tags</c>, <c>directory</c>, <c>people</c> — case-insensitive).
    /// </param>
    /// <param name="fallbackKey">
    /// The surface's <c>kw-l</c> key (<c>nav.home</c> / <c>nav.announcements</c>
    /// / … / <c>nav.people</c>), the floor the resolution falls back to (M29·3 /
    /// M29·4). The registry entries stay (ADR 0152 D3).
    /// </param>
    /// <param name="effectiveLanguage">
    /// The viewer's effective language (the <c>kw-l</c> fallback resolves in
    /// this language; a set admin label is shown in **all** languages — a
    /// single-string override, M29·8).
    /// </param>
    /// <param name="ct">Cancellation.</param>
    Task<string> GetLabelAsync(string surfaceKey, string fallbackKey,
                               string? effectiveLanguage,
                               CancellationToken ct = default);

    /// <summary>
    /// Saves the <c>SurfaceLabels</c> singleton — the ADR 0150 single audited
    /// write-lane shape (M29·5). Loads the existing row (a missing row is a
    /// **no-op** — the lane does not load-or-create; the seeder is the only
    /// writer that creates the row on a fresh boot, and a missing row degrades
    /// to the all-null in-code fallback, M29·6), applies the full 13-field set
    /// from <paramref name="labels"/>, and saves in one session (invariant C3).
    /// Exactly **one** <c>AccessAudit</c> row per call (<c>Via = Admin</c>,
    /// action <c>surface_labels.save</c>, <c>TargetKind</c>
    /// <c>"surface-labels"</c> — the <c>site.save</c> /
    /// <c>signup.set-open</c> / <c>timezone.set-default</c> singleton-toggle
    /// shape). **Strong consistency** (invariant C4): the new value is live on
    /// the very next <c>GetLabelAsync</c> / render. The lane **upserts** the
    /// singleton — it never creates a second row (M29·6).
    /// </summary>
    Task SaveAsync(SurfaceLabels labels, string actorBy, CancellationToken ct = default);
}
