namespace Kumunita.Core.SurfaceLabels;

/// <summary>
/// One row holding the admin's display-name override for each of the 13
/// top-navigation surfaces (ADR 0152, M29·6): a <b>singleton</b> — one row per
/// instance, <see cref="Id"/> fixed to the sentinel <c>singleton</c> (the exact
/// <c>SiteContent</c> / <c>LocaleSettings</c> shape, ADR 0005 B / ADR 0150), so
/// re-resolving it is a plain identity-keyed load. Each of the 13 fields is an
/// optional <c>string?</c> label override, defaulting to <c>null</c> (the
/// "use the <c>kw-l</c> fallback" shape, M29·3 / M29·4 — an unset or blank
/// label falls back to the surface's <c>kw-l</c> key in the viewer's
/// effective language, resolved by <see cref="ISurfaceLabelsService"/>).
/// <para>
/// The 13 fields are the <b>ceiling</b> for this milestone's scope (the ADR
/// 0152 D1 pin): a future <c>LBL-2</c> lane <b>adds</b> fields (additive per
/// ADR 0004 §B.1), it does not re-shape the existing 13. The label is a
/// <b>display override, never a re-route</b> (the "label, not re-route" pin —
/// a rename never moves a link, never changes a route, never touches an
/// <c>asp-route-*</c> / <c>href</c>); the nav + header views call one resolver
/// so they resolve to the <b>same</b> value (M29·1).
/// <para>
/// The <c>SiteContent</c> and <c>LocaleSettings</c> docs are <b>untouched</b>
/// — this is a new doc in a new bounded context (<c>Kumunita.Core.Surface
/// Labels</c>), not a new field on an existing one (ADR 0150 D6 / ADR 0006
/// module-boundary contract, M29·6).
/// </summary>
public sealed class SurfaceLabels
{
    /// <summary>
    /// The singleton identity sentinel (the exact <c>SiteContent</c> /
    /// <c>LocaleSettings</c> shape, ADR 0005 B) — one row per instance, fixed
    /// to <c>"singleton"</c> so a save never creates a second row (M29·6).
    /// </summary>
    public const string SingletonId = "singleton";

    /// <summary>
    /// The row identity. Fixed to <see cref="SingletonId"/> for the single
    /// per-instance row; do not reassign it (M29·6).
    /// </summary>
    public string Id { get; set; } = SingletonId;

    // ── the 13 surface label overrides (the ADR 0152 D1 ceiling) ──
    // Each is an optional string? override: null (or blank) = "use the
    // kw-l fallback" (M29·3 / M29·4). The label is text only — the nav icon
    // stays as shipped (M29·9); the HOME hero stays under the SITE lane
    // (M29·10), so this field drives the Home *nav label* only.

    /// <summary>The Home nav label (fallback <c>nav.home</c>, route <c>/</c>).
    /// Drives the nav item only — the SITE hero is untouched (M29·10).</summary>
    public string? Home { get; set; }

    /// <summary>The Announcements nav label (fallback <c>nav.announcements</c>,
    /// route <c>/announcements</c>).</summary>
    public string? Announcements { get; set; }

    /// <summary>The Community nav label (fallback <c>nav.community</c>, route
    /// <c>/community</c>).</summary>
    public string? Community { get; set; }

    /// <summary>The Groups nav label (fallback <c>nav.groups</c>, route
    /// <c>/groups</c>).</summary>
    public string? Groups { get; set; }

    /// <summary>The Events nav label (fallback <c>nav.events</c>, route
    /// <c>/events</c>).</summary>
    public string? Events { get; set; }

    /// <summary>The Projects nav label (fallback <c>nav.projects</c>, route
    /// <c>/projects/todos</c>).</summary>
    public string? Projects { get; set; }

    /// <summary>The Inventory nav label (fallback <c>inv.nav</c>, route
    /// <c>/inventory</c>).</summary>
    public string? Inventory { get; set; }

    /// <summary>The Bookmarks nav label (fallback <c>bm.nav</c>, route
    /// <c>/bookmarks</c>).</summary>
    public string? Bookmarks { get; set; }

    /// <summary>The Documents nav label (fallback <c>documents.title</c>, route
    /// <c>/documents</c>).</summary>
    public string? Documents { get; set; }

    /// <summary>The Pages nav label (fallback <c>nav.pages</c>, route
    /// <c>/pages</c>).</summary>
    public string? Pages { get; set; }

    /// <summary>The Tags nav label (fallback <c>nav.tags</c>, route
    /// <c>/tags</c>).</summary>
    public string? Tags { get; set; }

    /// <summary>The Directory nav label (fallback <c>nav.directory</c>, route
    /// <c>/directory</c>).</summary>
    public string? Directory { get; set; }

    /// <summary>The People nav label (fallback <c>nav.people</c>, route
    /// <c>/people</c>).</summary>
    public string? People { get; set; }

    /// <summary>
    /// Resolves this row's override for <paramref name="surfaceKey"/> (the
    /// closed 13-key set from design §2.2). <b>Case-insensitive</b>:
    /// <c>"home"</c> → <see cref="Home"/>, <c>"announcements"</c> →
    /// <see cref="Announcements"/>, …, <c>"people"</c> → <see cref="People"/>.
    /// Returns the stored value (which may be <c>null</c> or blank — the
    /// caller, <see cref="ISurfaceLabelsService.GetAsync"/>, decides whether to
    /// fall back to the <c>kw-l</c> key per M29·3), or <c>null</c> for an
    /// unrecognized key (the drift guard: no field outside the 13-key set, ADR
    /// 0152 D1).
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
