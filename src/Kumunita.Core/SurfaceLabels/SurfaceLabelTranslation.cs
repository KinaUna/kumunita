namespace Kumunita.Core.SurfaceLabels;

/// <summary>
/// A **user-added translation** of the <see cref="SurfaceLabels"/> singleton's
/// 13 surface labels into a language other than the one it was authored in
/// (ADR 0158; the <b>"LBL-2"</b> lane that ADR 0152 §D8 explicitly named:
/// "a future <c>LBL-2</c> lane would add a <c>SurfaceLabelTranslation</c> row
/// shape — the <c>PageTranslation</c> / <c>PostTranslation</c> precedent — + a
/// <c>/admin/labels</c> translation editor"). The exact
/// <see cref="Kumunita.Core.SiteContent.SiteContentTranslation"/> (ADR 0157)
/// shape carried onto the surface-labels surface.
/// <para>
/// The surface labels are a **singleton** (one row per instance, ADR 0152), so
/// a translation is keyed **only** on its <see cref="LanguageCode"/> — one row
/// per language, the <c>(LanguageCode)</c> unique index
/// (<see cref="SurfaceLabelsDocTypes.Configure"/>) enforces that at the
/// database layer (the <see cref="Kumunita.Core.Posts.PostTranslation"/>
/// <c>(PostId, LanguageCode)</c> convention, minus the parent key — the
/// surface-labels singleton is the parent, and it has exactly one identity).
/// The translation carries the **same 13 optional surface-label fields** as the
/// singleton (<see cref="SurfaceLabels"/> <c>Home</c> / <c>Announcements</c> /
/// … / <c>People</c>, all <c>string?</c>), and **at least one** must be
/// non-blank (the write seam enforces it, not the shape: a translation with
/// nothing in it is rejected — the ADR 0157 "at least one non-blank" rule
/// carried from the ADR 0026 group/community name+description rule). A blank
/// field on a translation row means "fall back to the singleton's value in
/// that language" (the ADR 0157 <c>SiteContentTranslation.HomeHeroEyebrow</c>
/// -optional shape, applied to all 13 surface-label fields); the kw-l key is
/// the floor below the singleton (the resolver's three-layer resolution, ADR
/// 0158 D5).
/// <para>
/// **Standing (ADR 0158):** the surface-labels singleton is owned by a
/// <b>GlobalAdmin</b> (the ADR 0152 <c>/admin/labels</c> write lane — the
/// <see cref="SurfaceLabels"/> doc has no per-resident owner standing), so a
/// translation is added / edited / removed by a <b>GlobalAdmin</b> only
/// (<see cref="Kumunita.Core.Authorization.AccessVia.Admin"/>). The
/// <see cref="Kumunita.Core.Identity.Roles.Translator"/> standing does **not**
/// qualify here: it is scoped to a parent's content lane (posts / groups /
/// announcements, ADR 0021), and the surface labels have no per-item parent
/// scope for it to bind to (the exact ADR 0157 D2 pin). The decision + its
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row are written by the
/// <see cref="SurfaceLabelsService"/> translation lanes in the caller's
/// transaction (C3).
/// <para>
/// **Not an authorization surface (the ADR 0152 read-pin carried over):**
/// like its parent singleton, a translation has **no own audience** — its
/// visibility inherits the surface-labels world-readable contract (the ADR
/// 0152 D3 public-surface pin). The Web reads it only on the label-resolution
/// path (a resident browsing the nav / a surface header in that language).
/// </para>
/// </summary>
public sealed class SurfaceLabelTranslation
{
    /// <summary>The surrogate document identity (the conventional <c>string Id</c>
    /// convention, ADR 0004 §B.1 — the <see cref="Kumunita.Core.Posts.PostTranslation.Id"/>
    /// / <see cref="Kumunita.Core.SiteContent.SiteContentTranslation.Id"/> shape; the
    /// business key is <see cref="LanguageCode"/>, enforced unique by the
    /// <see cref="SurfaceLabelsDocTypes.Configure"/> index).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The BCP-47 code of the language this translation is
    /// <b>in</b> (a <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/> — a
    /// supported, enabled language). Distinct from the singleton's authored-in
    /// text (the English <c>SurfaceLabels</c> defaults, ADR 0152 D5): that is the
    /// source; this is the language the translation renders the surface labels
    /// into.</summary>
    public string LanguageCode { get; set; } = string.Empty;

    // ── the 13 surface-label overrides (the ADR 0152 D1 ceiling, carried
    //    verbatim onto the per-language row) ──
    // Each is an optional string? override: null (or blank) = "fall back to
    // the singleton's value for that surface" (the ADR 0157 optional-field
    // shape); the kw-l key is the floor below the singleton (the resolver's
    // three-layer resolution, ADR 0158 D5).

    /// <summary>The translated Home label (optional — the singleton's
    /// <see cref="SurfaceLabels.Home"/> is the fallback when absent, then the
    /// <c>nav.home</c> kw-l key).</summary>
    public string? Home { get; set; }

    /// <summary>The translated Announcements label (optional — the singleton's
    /// <see cref="SurfaceLabels.Announcements"/> is the fallback when absent,
    /// then the <c>nav.announcements</c> kw-l key).</summary>
    public string? Announcements { get; set; }

    /// <summary>The translated Community label (optional — the singleton's
    /// <see cref="SurfaceLabels.Community"/> is the fallback when absent, then
    /// the <c>nav.community</c> kw-l key).</summary>
    public string? Community { get; set; }

    /// <summary>The translated Groups label (optional — the singleton's
    /// <see cref="SurfaceLabels.Groups"/> is the fallback when absent, then the
    /// <c>nav.groups</c> kw-l key).</summary>
    public string? Groups { get; set; }

    /// <summary>The translated Events label (optional — the singleton's
    /// <see cref="SurfaceLabels.Events"/> is the fallback when absent, then the
    /// <c>nav.events</c> kw-l key).</summary>
    public string? Events { get; set; }

    /// <summary>The translated Projects label (optional — the singleton's
    /// <see cref="SurfaceLabels.Projects"/> is the fallback when absent, then
    /// the <c>nav.projects</c> kw-l key).</summary>
    public string? Projects { get; set; }

    /// <summary>The translated Inventory label (optional — the singleton's
    /// <see cref="SurfaceLabels.Inventory"/> is the fallback when absent, then
    /// the <c>inv.nav</c> kw-l key).</summary>
    public string? Inventory { get; set; }

    /// <summary>The translated Bookmarks label (optional — the singleton's
    /// <see cref="SurfaceLabels.Bookmarks"/> is the fallback when absent, then
    /// the <c>bm.nav</c> kw-l key).</summary>
    public string? Bookmarks { get; set; }

    /// <summary>The translated Documents label (optional — the singleton's
    /// <see cref="SurfaceLabels.Documents"/> is the fallback when absent, then
    /// the <c>documents.title</c> kw-l key).</summary>
    public string? Documents { get; set; }

    /// <summary>The translated Pages label (optional — the singleton's
    /// <see cref="SurfaceLabels.Pages"/> is the fallback when absent, then the
    /// <c>nav.pages</c> kw-l key).</summary>
    public string? Pages { get; set; }

    /// <summary>The translated Tags label (optional — the singleton's
    /// <see cref="SurfaceLabels.Tags"/> is the fallback when absent, then the
    /// <c>nav.tags</c> kw-l key).</summary>
    public string? Tags { get; set; }

    /// <summary>The translated Directory label (optional — the singleton's
    /// <see cref="SurfaceLabels.Directory"/> is the fallback when absent, then
    /// the <c>nav.directory</c> kw-l key).</summary>
    public string? Directory { get; set; }

    /// <summary>The translated People label (optional — the singleton's
    /// <see cref="SurfaceLabels.People"/> is the fallback when absent, then the
    /// <c>nav.people</c> kw-l key).</summary>
    public string? People { get; set; }

    /// <summary>
    /// True when at least one of the 13 surface-label fields is non-blank
    /// (the ADR 0157 "at least one non-blank" rule, carried from the ADR 0026
    /// group/community name+description rule). The write seams
    /// (<c>AddTranslationAsync</c> / <c>UpdateTranslationAsync</c>) enforce it;
    /// this is the shape's read-only query.
    /// </summary>
    public bool HasAnyLabel() =>
        !string.IsNullOrWhiteSpace(Home) ||
        !string.IsNullOrWhiteSpace(Announcements) ||
        !string.IsNullOrWhiteSpace(Community) ||
        !string.IsNullOrWhiteSpace(Groups) ||
        !string.IsNullOrWhiteSpace(Events) ||
        !string.IsNullOrWhiteSpace(Projects) ||
        !string.IsNullOrWhiteSpace(Inventory) ||
        !string.IsNullOrWhiteSpace(Bookmarks) ||
        !string.IsNullOrWhiteSpace(Documents) ||
        !string.IsNullOrWhiteSpace(Pages) ||
        !string.IsNullOrWhiteSpace(Tags) ||
        !string.IsNullOrWhiteSpace(Directory) ||
        !string.IsNullOrWhiteSpace(People);

    /// <summary>The subject id of the actor who added the translation.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation was added (the initial timestamp; the
    /// edit lane updates the row in place and does not bump this — the
    /// ADR 0022 / ADR 0048 add-then-edit shape).</summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// Resolves this row's override for <paramref name="surfaceKey"/> (the
    /// closed 13-key set, the exact <see cref="SurfaceLabels.GetLabel"/>
    /// idiom). <b>Case-insensitive</b>: <c>"home"</c> → <see cref="Home"/>,
    /// <c>"announcements"</c> → <see cref="Announcements"/>, …, <c>"people"</c>
    /// → <see cref="People"/>. Returns the stored value (which may be
    /// <c>null</c> or blank — the resolver decides whether to fall back to the
    /// singleton, then the kw-l key, per ADR 0158 D5), or <c>null</c> for an
    /// unrecognized key (the drift guard: no field outside the 13-key set,
    /// ADR 0152 D1).
    /// </summary>
    public string? GetLabel(string surfaceKey)
    {
        if (string.IsNullOrWhiteSpace(surfaceKey))
        {
            return null;
        }

        return surfaceKey.Trim().ToLowerInvariant() switch
        {
            "home"        => Home,
            "announcements" => Announcements,
            "community"   => Community,
            "groups"      => Groups,
            "events"      => Events,
            "projects"    => Projects,
            "inventory"   => Inventory,
            "bookmarks"   => Bookmarks,
            "documents"   => Documents,
            "pages"       => Pages,
            "tags"        => Tags,
            "directory"   => Directory,
            "people"      => People,
            _             => null // an unrecognized key — the 13-key ceiling (ADR 0152 D1)
        };
    }
}
