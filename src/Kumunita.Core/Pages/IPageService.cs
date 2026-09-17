using Marten;

namespace Kumunita.Core.Pages;

/// <summary>
/// The <c>/pages</c> bounded-context's service seam (the Pages lane
/// <c>PG</c> — ADR 0039). The public surface of <see cref="PageService"/>.
/// <para>
/// **U01 (this unit) is the structure seam only — no methods yet.** The
/// read lanes (<c>GetByPathAsync</c> / <c>GetTreeAsync</c> /
/// <c>GetByMountPointAsync</c> / <c>GetTranslationsAsync</c> /
/// <c>GetBySlugUnderParentAsync</c> + the <c>PageToAuditableResource</c>
/// adapter + the standing matrix) land in U02; the write lanes
/// (<c>CreateAsync</c> / <c>UpdateAsync</c> / <c>PublishAsync</c> /
/// <c>MoveAsync</c> / <c>DeleteAsync</c> / <c>AddTranslationAsync</c>, each
/// with its C3 <see cref="Authorization.AccessAudit"/> row) land in U03. The
/// interface + the DI registration below are the load-bearing part of U01 —
/// the Web-side consumer (the U04 <c>PageController</c>) will resolve this
/// seam, and a test double (NSubstitute) can drive it without a live Postgres
/// (the <see cref="Announcements.IAnnouncementService"/> convention: a
/// store-composing service kept behind an interface so the controller tests
/// substitute).
/// </para>
/// <para>
/// **Standing (ADR 0039 §3.7):** every write lane re-checks standing
/// server-side in the <see cref="PageService"/> (the
/// <see cref="Announcements.AnnouncementService.CreateAsync"/> C3 pattern) —
/// a Web <c>[Authorize(Roles=…)]</c> is a convenience pre-gate, not the
/// source of truth.
/// </para>
/// </summary>
public interface IPageService
{
    /// <summary>
    /// Resolve a **derived path** (<c>a/b/c</c>) to the <see cref="Page"/> it
    /// names (ADR 0039 §3.3 — the path is the chain of ancestor slugs, not
    /// stored). Walks the forest root-to-leaf following
    /// <see cref="Page.ParentId"/>/<see cref="Page.Slug"/>. A deleted page
    /// (<see cref="Page.IsDeleted"/>) is treated as absent (the ADR 0024
    /// soft-delete read filter). A missing path segment is a
    /// <see cref="KeyNotFoundException"/> (the Web layer's 404).
    /// </summary>
    Task<Page> GetByPathAsync(string path);

    /// <summary>
    /// One level of the tree (the <c>(ParentId, Slug)</c> unique key, ADR 0039
    /// §3.2): the child of <paramref name="parentId"/> (a root when null) whose
    /// <see cref="Page.Slug"/> is <paramref name="slug"/>. A deleted page is
    /// treated as absent; a missing row is a <see cref="KeyNotFoundException"/>.
    /// </summary>
    Task<Page> GetBySlugUnderParentAsync(string? parentId, string slug);

    /// <summary>
    /// The forest (the roots + their descendants, ADR 0039 §3.3), **filtered
    /// to non-deleted pages** (<see cref="Page.IsDeleted"/> excluded — the
    /// ADR 0024 soft-delete read filter; the single place a deleted page could
    /// leak into the browse view, so the filter lives with the read). Ordered
    /// by <see cref="Page.Created"/> ascending, then <see cref="Page.Slug"/>
    /// (a stable tree order the U04 tree browse can render).
    /// </summary>
    Task<IReadOnlyList<Page>> GetTreeAsync();

    /// <summary>
    /// The user-added translations of a page (the ADR 0022 read), ordered by
    /// <see cref="PageTranslation.LanguageCode"/> ascending (the
    /// <see cref="Posts.PostTranslation"/> / <see cref="Announcements
    /// .AnnouncementTranslation"/> read shape). A missing page is a
    /// <see cref="KeyNotFoundException"/>; a page with no translation rows is
    /// an empty list (the authored-in body is the default, not a row).
    /// </summary>
    Task<IReadOnlyList<PageTranslation>> GetTranslationsAsync(string pageId);

    /// <summary>
    /// The page mounted at a UI slot (ADR 0039 §3.3/§3.8 — e.g.
    /// <c>"footer/community"</c>, <c>"help/account"</c>): the one non-deleted
    /// page whose <see cref="Page.MountPoint"/> equals <paramref name="slot"/>.
    /// Returns <c>null</c> when the slot is unmounted (the Web layer skips the
    /// link) — a mount point is a display concern, not an access boundary, so
    /// there is no <see cref="KeyNotFoundException"/> here.
    /// </summary>
    Task<Page?> GetByMountPointAsync(string slot);

    // ─── Write lanes (ADR 0039 §3.7 — U03) ────────────────────────────────
    //
    // Each re-checks standing **server-side** (the C3 single-source pin — a
    // Web [Authorize(Roles=…)] is a convenience pre-gate, not the source of
    // truth) and stores its AccessAudit row in the **caller's** in-flight
    // IDocumentSession (invariant C3, ADR 0006 — synchronous, in-transaction,
    // not a Wolverine side effect) with TargetKind = "page" and the §3.7
    // action name. The standing-matrix gate helpers (CheckCreateStanding /
    // CheckEditStanding / CheckTranslateStanding) and the cycle-guard /
    // depth-cap helpers the write lanes call live on the concrete
    // PageService as public, testable helpers (U02) — they are not part of
    // the Web-facing surface the U04 PageController resolves.

    /// <summary>
    /// Creates a <see cref="Page"/> in the **caller's** in-flight session
    /// (C3). Standing (§3.7): a GlobalAdmin (<see cref="Authorization
    /// .AccessVia.Admin"/>) or a community Moderator scoped to
    /// <see cref="Page.ComponentId"/> (<see cref="Authorization
    /// .AccessVia.Moderator"/>); a plain Member is denied
    /// (<see cref="UnauthorizedAccessException"/>, the Web layer's 403).
    /// A root-slug collision (two roots sharing a <see cref="Page.Slug"/>)
    /// is a <see cref="InvalidOperationException"/> — the
    /// <c>(ParentId, Slug)</c> unique index does **not** prevent it (Postgres
    /// treats NULLs as distinct), so this lane is the authoritative root-slug
    /// guard. A denied actor throws **before** anything is stored.
    /// </summary>
    Task<Page> CreateAsync(
        Page page, string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);

    /// <summary>
    /// Edits an existing <see cref="Page"/> (title / body / audience /
    /// hierarchy fields) in the **caller's** in-flight session (C3). Standing
    /// (§3.7): the page's <see cref="Page.AuthorId"/> (<see cref="Authorization
    /// .AccessVia.Owner"/>), a GlobalAdmin (<see cref="Authorization
    /// .AccessVia.Admin"/>), or a community Moderator scoped to
    /// <see cref="Page.ComponentId"/> (<see cref="Authorization
    /// .AccessVia.Moderator"/>). A missing page is a
    /// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied
    /// actor is a <see cref="UnauthorizedAccessException"/> (403).
    /// </summary>
    Task<Page> UpdateAsync(
        Page updated, string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);

    /// <summary>
    /// Publishes a draft <see cref="Page"/> — **author-only** (ADR 0037 pin):
    /// the sole decision is <c>Page.AuthorId == actorId</c> (ordinal); a
    /// non-author is a hard <see cref="UnauthorizedAccessException"/> (the Web
    /// layer's 403) **even at GlobalAdmin** (publishing is the author's
    /// choice, not an admin's lever). Idempotent: a second publish on an
    /// already-live page does not stamp <see cref="Page.Modified"/>.
    /// </summary>
    Task<Page> PublishAsync(string pageId, string actorId, IDocumentSession session);

    /// <summary>
    /// Moves a <see cref="Page"/> under a new parent (reparent) in the
    /// **caller's** in-flight session (C3). Standing (§3.7): a GlobalAdmin or
    /// a community Moderator scoped to <see cref="Page.ComponentId"/> —
    /// **not** a plain author (a page is platform content, not a personal
    /// note; <see cref="AccessVia"/> is <c>Admin</c> / <c>Moderator</c>,
    /// never <c>Owner</c>). Applies the **cycle-guard** (the new parent is
    /// not a descendant of the page) and the **depth-cap** (chain ≤
    /// <c>MaxDepth</c>, 8); the derived path is rewritten by the single
    /// <see cref="Page.ParentId"/> / <see cref="Page.Slug"/> column write
    /// (paths are not stored). A <c>newSlug</c> change is authoritative for
    /// root-level uniqueness (the index does not guard it).
    /// </summary>
    Task<Page> MoveAsync(
        string pageId, string? newParentId, string? newSlug,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);

    /// <summary>
    /// **Soft-deletes** a <see cref="Page"/> in the **caller's** in-flight
    /// session (C3): sets <see cref="Page.IsDeleted"/> to <c>true</c> and
    /// stamps <see cref="Page.Modified"/> — it does **not** remove the row or
    /// orphan its children (the ADR 0024 shape; the U02 read lanes filter the
    /// flag out). Standing (§3.7): a GlobalAdmin or a community Moderator
    /// scoped to <see cref="Page.ComponentId"/> — **not** a plain author
    /// (<see cref="AccessVia"/> is <c>Admin</c> / <c>Moderator</c>, never
    /// <c>Owner</c>).
    /// </summary>
    Task DeleteAsync(
        string pageId, string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);

    /// <summary>
    /// Adds a **user-added translation** of a page in the **caller's**
    /// in-flight session (C3; the ADR 0029 standing carried over, design doc
    /// §3.7). Standing: a GlobalAdmin (<see cref="Authorization
    /// .AccessVia.Admin"/>), a Translator (<see cref="Authorization
    /// .AccessVia.Admin"/>), or a community Moderator scoped to
    /// <see cref="Page.ComponentId"/> (<see cref="Authorization
    /// .AccessVia.Moderator"/>); a flat/public page has no community to
    /// moderate, so the component-moderator standing does not qualify.
    /// The <c>(PageId, LanguageCode)</c> unique index (U01) is the add-only
    /// duplicate guard. A missing page is a
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is a
    /// <see cref="UnauthorizedAccessException"/> (403).
    /// </summary>
    Task<PageTranslation> AddTranslationAsync(
        string pageId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);
}
