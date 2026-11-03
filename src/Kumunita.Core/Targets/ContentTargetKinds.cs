namespace Kumunita.Core.Targets;

/// <summary>
/// The pure content-target kind table (IMPROVE-CODE C05 — the seam-linking
/// of the Bookmark / PersonalNote target resolvers, ADR 0118 D1/D3/D5 +
/// ADR 0167 D1/D7). A static class named <c>*Kinds</c>, which is a
/// legal D6 boundary target (a module may reference a static class whose
/// name ends <c>Kinds</c> from another module — the gate rule in
/// <c>CodeMetrics.IsAllowedBoundaryTarget</c>).
/// <para>
/// This type holds **no** session, **no** DI, **no** I/O: the closed
/// kind sets per surface, the static <c>page-*</c> rows, and the link
/// templates for the simple kinds. It is the **unit-testable** part of the
/// resolver (the <c>ContentTargetTableTests</c> seam-test). The
/// session-touching loaders live in <see cref="ContentTargetResolver"/>.
/// </para>
/// <para>
/// **Byte-identical behaviour** is the contract: the same kinds, the same
/// <c>CanAsync(Read)</c> decisions, the same <c>Title</c> / <c>Link</c> /
/// <c>Visible</c> shape, the same audit rows as the private clones this
/// replaces. The two surfaces' divergences (the <c>project</c> link,
/// the <c>announcement</c> list draft gate, the <c>grouppost</c> /
/// <c>document</c> / <c>page-*</c> kinds) are recorded here, not
/// re-derived per surface.
/// </para>
/// </summary>
public static class ContentTargetKinds
{
    // ── Surface constants (the requesting surface, passed to the resolver) ──

    /// <summary>The M17 Bookmarks surface (ADR 0118) — 9 kinds.</summary>
    public const string Bookmarks = "bookmarks";

    /// <summary>The M37 PersonalNotes surface (ADR 0167) — 21 kinds.</summary>
    public const string PersonalNotes = "personal-notes";

    // ── Closed kind sets (D1 — the M5 KanbanStatuses string-not-enum shape) ──

    /// <summary>
    /// The M17 closed <c>TargetKind</c> set (ADR 0118 D1 + amendment).
    /// A kind outside this set is a drift event (ADR 0118 §2.7) and
    /// resolves to "not visible" / "(null, null)".
    /// </summary>
    public static readonly IReadOnlySet<string> BookmarksKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "post", "event", "todo", "announcement", "page",
            "project", "board", "inventory", "directory",
        };

    /// <summary>
    /// The M37 closed <c>TargetKind</c> set (ADR 0167 D1 + amendment).
    /// The same 9 kinds as <see cref="BookmarksKinds"/> **plus**
    /// <c>grouppost</c>, <c>document</c>, and the 11 <c>page-*</c> kinds
    /// from the ADR 0167 amendment (2026-10-25). A kind outside this set
    /// is a drift event and resolves to "not visible" / "(null, null)".
    /// </summary>
    public static readonly IReadOnlySet<string> PersonalNotesKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "post", "grouppost", "event", "page", "project",
            "todo", "board", "document", "directory", "announcement", "inventory",
            // 2026-10-25 page-level notes lane (ADR 0167 amendment):
            "page-announcements", "page-community", "page-group",
            "page-events", "page-projects", "page-project",
            "page-todos", "page-boards", "page-inventory", "page-documents",
        };

    /// <summary>
    /// The canonical order of the M17 closed set (for group ordering in the
    /// list — the <c>BookmarkService.ClosedTargetKindsOrdered</c> shape).
    /// </summary>
    public static readonly string[] BookmarksOrdered =
        ["post", "event", "todo", "announcement", "page", "project", "board", "inventory", "directory"];

    /// <summary>
    /// The canonical order of the M37 closed set (for group ordering in the
    /// list — the <c>PersonalNoteService.ClosedTargetKindsOrdered</c> shape).
    /// </summary>
    public static readonly string[] PersonalNotesOrdered =
        ["post", "grouppost", "event", "page", "project",
         "todo", "board", "document", "directory", "announcement", "inventory",
         "page-announcements", "page-community", "page-group",
         "page-events", "page-projects", "page-project",
         "page-todos", "page-boards", "page-inventory", "page-documents"];

    // ── Kind-set membership ──────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when <paramref name="kind"/> is in the closed set
    /// for <paramref name="surface"/>. A kind outside the set is a drift
    /// event (ADR 0118 §2.7) and must resolve to "not visible".
    /// </summary>
    public static bool IsValid(string kind, string surface)
    {
        return surface switch
        {
            Bookmarks => BookmarksKinds.Contains(kind),
            PersonalNotes => PersonalNotesKinds.Contains(kind),
            _ => false,
        };
    }

    // ── Static rows (the ADR 0167 amendment page-* kinds) ────────────────────
    // These kinds are always visible (the page itself is the surface — no
    // item-level visibility to re-check). The targetId is ignored (or is
    // "static" for page-group). The title + link are fixed.

    private static readonly Dictionary<string, (string Title, string Link)> StaticRows =
        new(StringComparer.Ordinal)
        {
            ["page-announcements"] = ("Announcements", "/announcements"),
            ["page-community"] = ("Community", "/community"),
            ["page-events"] = ("Events", "/events"),
            ["page-projects"] = ("Projects", "/projects"),
            ["page-todos"] = ("To-dos", "/projects/todos"),
            ["page-boards"] = ("Boards", "/projects/boards"),
            ["page-inventory"] = ("Inventory", "/inventory"),
            ["page-documents"] = ("Documents", "/documents"),
        };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="kind"/> is a static
    /// <c>page-*</c> row (the ADR 0167 amendment: the page itself is the
    /// surface, always visible, fixed title + link). The
    /// <paramref name="row"/> is the fixed (<c>Title</c>, <c>Link</c>).
    /// </summary>
    public static bool TryStaticRow(string kind, out (string Title, string Link) row)
    {
        if (StaticRows.TryGetValue(kind, out row)) { return true; }
        row = (string.Empty, string.Empty);
        return false;
    }

    /// <summary>
    /// All static <c>page-*</c> kind → (Title, Link) pairs (for the
    /// unit test to enumerate every static row).
    /// </summary>
    public static IReadOnlyDictionary<string, (string Title, string Link)> AllStaticRows
        => StaticRows;

    // ── Link templates (the simple kinds — no session needed) ────────────────

    /// <summary>The link for a <c>post</c> target.</summary>
    public static string PostLink(string targetId) => $"/posts/{targetId}";

    /// <summary>The link for a <c>grouppost</c> target.</summary>
    public static string GrouppostLink(string groupId, string targetId) => $"/groups/{groupId}/posts/{targetId}";

    /// <summary>The link for an <c>event</c> target.</summary>
    public static string EventLink(string targetId) => $"/events/{targetId}";

    /// <summary>The link for a <c>todo</c> target.</summary>
    public static string TodoLink(string targetId) => $"/projects/todos/{targetId}";

    /// <summary>
    /// The link for a <c>project</c> target, per surface. The M17 surface
    /// links the project detail page (<c>/projects/projects/{id}</c>);
    /// the M37 surface links the projects list page (<c>/projects/{id}</c>)
    /// — the ADR 0118 amendment shape re-applied. **Byte-identical
    /// behaviour** is preserved: the divergence is recorded here, not
    /// re-derived per surface.
    /// </summary>
    public static string ProjectLink(string targetId, string surface) =>
        surface == Bookmarks
            ? $"/projects/projects/{targetId}"
            : $"/projects/{targetId}";

    /// <summary>The link for a <c>board</c> target.</summary>
    public static string BoardLink(string targetId) => $"/projects/boards/{targetId}";

    /// <summary>The link for an <c>announcement</c> target.</summary>
    public static string AnnouncementLink(string targetId) => $"/announcements/{targetId}";

    /// <summary>The link for an <c>inventory</c> target.</summary>
    public static string InventoryLink(string targetId) => $"/inventory/{targetId}";

    /// <summary>The link for a <c>directory</c> target.</summary>
    public static string DirectoryLink(string targetId) => $"/directory/{targetId}";

    /// <summary>The link for a <c>document</c> target (M37 only).</summary>
    public static string DocumentLink(string targetId) => $"/documents/{targetId}";

    /// <summary>The link for a <c>page-group</c> target (dynamic group id).</summary>
    public static string GroupLink(string targetId) => $"/groups/{targetId}";

    /// <summary>
    /// The link for a <c>page-project</c> target (the project's own page,
    /// the <c>/projects/projects/{id}</c> detail — M37 only).
    /// </summary>
    public static string PageProjectLink(string targetId) => $"/projects/projects/{targetId}";

    /// <summary>The <c>page-group</c> static id (the /groups index page).</summary>
    public const string PageGroupStaticId = "static";
}
