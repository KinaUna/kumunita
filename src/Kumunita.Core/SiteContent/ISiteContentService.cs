namespace Kumunita.Core.SiteContent;

/// <summary>
/// The read + write seams for the <c>SiteContent</c> singleton (ADR 0150).
/// The <c>GetAsync</c> read is the ADR 0050 <c>IsSignupOpenAsync</c>
/// best-effort shape (missing row / read failure degrades to the in-code
/// fallback — never throws, never returns null); the <c>SaveAsync</c> write
/// is the ADR 0050 <c>SetSignupOpenAsync</c> single audited write-lane
/// shape (one session, one <c>AccessAudit</c> row, strong consistency).
/// </summary>
public interface ISiteContentService
{
    /// <summary>
    /// Loads the <c>SiteContent</c> singleton. **Best-effort**: a missing
    /// store, a missing row (a fresh boot before the seeder ran), or a read
    /// failure degrades to the **in-code fallback** (a fresh
    /// <see cref="SiteContent"/> with every field at its shipped default —
    /// the byte-identical <c>kw-l</c> text + every section shown). The read
    /// is a **public landing surface** (SITE·1) — never an access decision,
    /// never a claim, never audited.
    /// </summary>
    Task<SiteContent> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the <c>SiteContent</c> singleton — the ADR 0050
    /// <c>SetSignupOpenAsync</c> single audited write-lane shape. Loads the
    /// existing row (a missing row is a **no-op** — the lane does not
    /// load-or-create; a fresh instance is already at the in-code fallback,
    /// and the seeder is the only writer that creates the row), applies the
    /// full 13-field set from <paramref name="content"/>, and saves in one
    /// session (invariant C3). Exactly **one** <c>AccessAudit</c> row per
    /// call (<c>Via = Admin</c>, action <c>site.save</c>, <c>TargetKind</c>
    /// "site" — the <c>signup.set-open</c> / <c>timezone.set-default</c> /
    /// <c>dateformat.set-default</c> singleton-toggle shape, SITE·2).
    /// **Strong consistency** (invariant C4): the new value is live on the
    /// very next <c>GetAsync</c> / render. The lane **upserts** the
    /// singleton — it never creates a second row (SITE·6).
    /// </summary>
    Task SaveAsync(SiteContent content, string actorBy, CancellationToken ct = default);

    // ── ADR 0157 — the SITE-2 hero-translation lanes (the ADR 0150 §D5
    //    "future SITE-2 translation lane"; the ADR 0022 post-translation
    //    shape, add-only + edit + remove on the same (LanguageCode) key) ──

    /// <summary>
    /// Loads every <see cref="SiteContentTranslation"/> row (all languages).
    /// A **public landing-surface read** (ADR 0150 D2) — world-readable, never
    /// an access decision, never audited (the ADR 0022
    /// <c>GetPostTranslationsAsync</c> "a read, not a decision" pin).
    /// Ordered by <c>LanguageCode</c>. A read failure degrades to an empty
    /// list (the hero renders the singleton's value — never a blank page).
    /// </summary>
    Task<IReadOnlyList<SiteContentTranslation>> GetTranslationsAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a **user-added translation** of the singleton's hero text into
    /// <paramref name="languageCode"/> (ADR 0157; the ADR 0022
    /// <see cref="Posts.PostService.AddPostTranslationAsync"/> shape, minus
    /// the parent standing — the site has no per-resident owner, so the
    /// standing is a GlobalAdmin only, enforced by the Web gate, ADR 0150
    /// D8). **At least one** of the four hero fields must be non-blank
    /// (a translation with nothing in it is rejected, the ADR 0026 "at
    /// least one non-blank" rule); blank fields are stored as
    /// <c>null</c> (the singleton's value is the fallback for that field).
    /// Re-adding a language **overwrites** that row (upsert, the ADR 0048
    /// "re-adding a language overwrites that row" shape) — the
    /// <c>(LanguageCode)</c> unique index enforces one row per language.
    /// Exactly one <c>AccessAudit</c> row per call (action
    /// <c>sitetranslation.add</c>, <c>Via = Admin</c>). **Strong
    /// consistency** (invariant C4): live on the very next render in that
    /// language.
    /// </summary>
    Task<SiteContentTranslation> AddTranslationAsync(
        string languageCode,
        string? homeHeroEyebrow,
        string? homeHeroLead,
        string? aboutHeroEyebrow,
        string? aboutHeroLead,
        string actorBy,
        CancellationToken ct = default);

    /// <summary>
    /// **Updates** the existing <see cref="SiteContentTranslation"/> row for
    /// <paramref name="languageCode"/> (ADR 0157; the ADR 0048
    /// <see cref="Posts.PostService.UpdatePostTranslationAsync"/> shape). The
    /// four hero fields are replaced verbatim (a blank field clears that
    /// field's override → the singleton's value is the fallback again);
    /// **at least one** must be non-blank (a caller error otherwise). A
    /// missing row is a <see cref="KeyNotFoundException"/>. One
    /// <c>AccessAudit</c> row (action <c>sitetranslation.update</c>,
    /// <c>Via = Admin</c>).
    /// </summary>
    Task<SiteContentTranslation> UpdateTranslationAsync(
        string languageCode,
        string? homeHeroEyebrow,
        string? homeHeroLead,
        string? aboutHeroEyebrow,
        string? aboutHeroLead,
        string actorBy,
        CancellationToken ct = default);

    /// <summary>
    /// **Removes** the existing <see cref="SiteContentTranslation"/> row for
    /// <paramref name="languageCode"/> (ADR 0157; the ADR 0048
    /// <see cref="Posts.PostService.RemovePostTranslationAsync"/> shape). A
    /// hard delete of the row — the trail is preserved by the
    /// <c>AccessAudit</c> row written in the same session (action
    /// <c>sitetranslation.remove</c>, <c>Via = Admin</c>); the hero reverts to
    /// the singleton's value in that language. A missing row is a
    /// <see cref="KeyNotFoundException"/> (a double-remove is a shape error
    /// for this route — the Web offers the remove affordance only for
    /// languages that have a row).
    /// </summary>
    Task RemoveTranslationAsync(string languageCode, string actorBy, CancellationToken ct = default);
}
