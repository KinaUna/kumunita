namespace Kumunita.Core.SurfaceLabels;

/// <summary>
/// The read + write seams for the <c>SurfaceLabels</c> singleton (ADR 0152,
/// M29). The <c>GetLabelAsync</c> read is the shared label-resolver the nav +
/// header views call (M29·1) — best-effort (a missing row / read failure
/// degrades to the <c>kw-l</c> fallback, never throws, never blank),
/// world-readable, never audited (M29·2, ADR 0001-B thin-token). The
/// <c>GetAsync</c> read returns the **stored** label set (the raw override
/// fields, never the resolved text) for the <c>/admin/labels</c> form seed
/// (ADR 0152, M29·7 — best-effort, never null, never audited). The
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
    /// so they resolve to the **same** value). The three-layer resolution
    /// (ADR 0158 D5): the <see cref="SurfaceLabelTranslation"/> override for
    /// this surface in <paramref name="effectiveLanguage"/> **if present and
    /// non-blank** → the <see cref="SurfaceLabels"/> singleton override
    /// **if present and non-blank** → the <c>kw-l</c> translation of
    /// <paramref name="fallbackKey"/> in <paramref name="effectiveLanguage"/>
    /// (the "empty = use default" shape, M29·3 / M29·4; a fresh instance with
    /// no translation row and no singleton override resolves exactly the same
    /// as it did before ADR 0158, so a no-op translation surface is
    /// byte-identical to today, ADR 0158 D9). **Best-effort**: a missing row
    /// (a fresh boot before the seeder ran), a missing store, or a read
    /// failure degrades to the <c>kw-l</c> fallback — never throws, never
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
    /// The viewer's effective language. Drives the <c>kw-l</c> fallback and
    /// selects the <see cref="SurfaceLabelTranslation"/> row for this
    /// surface (a translation is keyed on <c>LanguageCode</c>, ADR 0158).
    /// A set **singleton** admin label is shown in **all** languages — a
    /// single-string override (M29·8) — and wins over the <c>kw-l</c>
    /// floor; the three-layer resolution is: the
    /// <see cref="SurfaceLabelTranslation"/> override for this language
    /// (if non-blank) → the <see cref="SurfaceLabels"/> singleton override
    /// (if non-blank) → the <c>kw-l</c> key resolved in this language
    /// (ADR 0158 D5).
    /// </param>
    /// <param name="ct">Cancellation.</param>
    Task<string> GetLabelAsync(string surfaceKey, string fallbackKey,
                               string? effectiveLanguage,
                               CancellationToken ct = default);

    /// <summary>
    /// Loads the <c>SurfaceLabels</c> singleton's **stored** label set (the
    /// raw override fields, **not** the resolved text) for the
    /// <c>/admin/labels</c> form seed (ADR 0152, M29·7). **Best-effort**: a
    /// missing store, a missing row (a fresh boot before the seeder ran), or a
    /// read failure degrades to the **in-code fallback** — a fresh
    /// <see cref="SurfaceLabels"/> with every label field <c>null</c> (the
    /// all-null = "use the <c>kw-l</c> fallback" shape, M29·3 / M29·4). The
    /// read is **never null** (the form always renders) and **never audited**
    /// (M29·2 — the same world-readable read shape as <c>GetLabelAsync</c>,
    /// the <see cref="ISiteContentService.GetAsync"/> / ADR 0050
    /// <c>IsSignupOpenAsync</c> best-effort shape).
    /// <para>
    /// <b>Why this seam exists (the U08 drift fix):</b> the register's
    /// <c>/admin/labels</c> GET says "seed the form with the current
    /// singleton" but the interface only had <c>GetLabelAsync</c> — which
    /// returns the **resolved** text (admin label <b>or</b> the
    /// <c>kw-l</c> fallback). Seeding the form via <c>GetLabelAsync</c> would
    /// put the fallback text (e.g. "Announcements") into a blank field, and a
    /// save would then **store** that text as the admin label (a regression —
    /// an admin who never set a label would "lock in" the fallback). This seam
    /// returns the **stored** value so a blank field stays blank and a save
    /// clears (the "empty = use default" shape, M29·3).
    /// </para>
    /// </summary>
    Task<SurfaceLabels> GetAsync(CancellationToken ct = default);

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

    // ── ADR 0158 — the LBL-2 surface-label translation lanes (the ADR 0152
    //    §D8 "future LBL-2 translation lane"; the ADR 0157 SITE-2
    //    hero-translation shape, carried onto the surface-labels surface):
    //    add a translation into a language, edit the existing row for a
    //    language, or remove it. ───────────────────────────────────────────

    /// <summary>
    /// Loads every <see cref="SurfaceLabelTranslation"/> row (all languages).
    /// A **public surface read** (ADR 0152 D3) — world-readable, never an
    /// access decision, never audited (the ADR 0157
    /// <c>GetSiteContentTranslationsAsync</c> "a read, not a decision" pin,
    /// carried from the ADR 0022 <c>GetPostTranslationsAsync</c> idiom).
    /// Ordered by <c>LanguageCode</c>. A read failure degrades to an empty
    /// list (the labels resolve the singleton's value / the kw-l floor —
    /// never a blank nav).
    /// </summary>
    Task<IReadOnlyList<SurfaceLabelTranslation>> GetTranslationsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Adds (or overwrites, the upsert shape) a translation of the 13
    /// surface labels into <paramref name="languageCode"/> (ADR 0158; the
    /// ADR 0157 <c>SiteContentService.AddTranslationAsync</c> shape, minus
    /// the parent standing — the surface labels have no per-resident owner,
    /// so the standing is a GlobalAdmin only, enforced by the Web gate, ADR
    /// 0152 D8). At least one of the 13 label fields must be non-blank (the
    /// write seam rejects an empty translation — the ADR 0157 "at least one
    /// non-blank" rule); a blank field leaves that surface at the singleton's
    /// value (the fallback). One <c>AccessAudit</c> row
    /// (<c>surface_labels_translation.add</c>), strong-consistency (live on
    /// the next render in that language).
    /// </summary>
    Task<SurfaceLabelTranslation> AddTranslationAsync(
        string languageCode,
        SurfaceLabelTranslation labels,
        string actorBy,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the existing translation for <paramref name="languageCode"/>
    /// (ADR 0158; the ADR 0157
    /// <c>SiteContentService.UpdateTranslationAsync</c> shape). The 13 label
    /// fields are replaced verbatim (a blank field clears that surface's
    /// override → the singleton's value is the fallback again); at least one
    /// must be non-blank. A missing row is a <see cref="KeyNotFoundException"/>
    /// (a double-update is a shape error for this route — the page offers the
    /// edit affordance only for languages that have a row). One
    /// <c>AccessAudit</c> row (<c>surface_labels_translation.update</c>).
    /// </summary>
    Task<SurfaceLabelTranslation> UpdateTranslationAsync(
        string languageCode,
        SurfaceLabelTranslation labels,
        string actorBy,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the existing translation for <paramref name="languageCode"/>
    /// (ADR 0158; the ADR 0157
    /// <c>SiteContentService.RemoveTranslationAsync</c> shape). The surface
    /// labels revert to the singleton's value in that language (the kw-l
    /// floor below). A missing row is a <see cref="KeyNotFoundException"/>
    /// (a double-remove is a shape error for this route — the page offers the
    /// remove affordance only for languages that have a row). One
    /// <c>AccessAudit</c> row (<c>surface_labels_translation.remove</c>).
    /// </summary>
    Task RemoveTranslationAsync(
        string languageCode,
        string actorBy,
        CancellationToken ct = default);
}
