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
    /// Resolve a page **and** its translations in one read (the ADR 0043 D7 /
    /// ADR 0044 D5 read seam): the <see cref="Page"/> at
    /// <paramref name="path"/> plus its <see cref="PageTranslation"/> rows
    /// (ordered by <see cref="PageTranslation.LanguageCode"/> ascending, the
    /// <see cref="GetTranslationsAsync"/> shape). A single session, so the
    /// caller gets a consistent (page, translations) pair — the
    /// <c>StaticPagesController</c> uses this to pick the body for the
    /// request's effective language (the provider chain) instead of two
    /// independent reads that could drift. A missing page is a
    /// <see cref="KeyNotFoundException"/>; a page with no translation rows
    /// resolves to an empty list (the authored-in body is the default).
    /// </summary>
    Task<(Page Page, IReadOnlyList<PageTranslation> Translations)> ResolvePageAsync(
        string path);

    /// <summary>
    /// The page mounted at a UI slot (ADR 0039 §3.3/§3.8 — e.g.
    /// <c>"footer/community"</c>, <c>"help/account"</c>): the one non-deleted
    /// page whose <see cref="Page.MountPoint"/> equals <paramref name="slot"/>.
    /// Returns <c>null</c> when the slot is unmounted (the Web layer skips the
    /// link) — a mount point is a display concern, not an access boundary, so
    /// there is no <see cref="KeyNotFoundException"/> here.
    /// </summary>
    Task<Page?> GetByMountPointAsync(string slot);

    // ─── ADR 0040 — blog lanes (per-user feed + ownership guard) ─────────

    /// <summary>
    /// Returns the actor's own <see cref="PageKind.User"/> (blog) root page
    /// (a root-level, non-deleted user page whose <c>AuthorId</c> is
    /// <paramref name="actorId"/>), or <c>null</c> when the actor has no blog
    /// yet. Used by the Web composer's "My blog" parent-picker and the
    /// <c>/blog/{userId}</c> feed (which lists pages under this root).
    /// </summary>
    Task<Page?> GetBlogRootAsync(string actorId);

    /// <summary>
    /// Returns the active, non-deleted <see cref="PageKind.User"/> (blog)
    /// pages of <paramref name="authorId"/> (all levels — the feed shows the
    /// blog's full content in Modified-desc order), or an empty list when the
    /// actor has no published blog content (the feed renders the "no posts
    /// yet" empty state).
    /// </summary>
    Task<IReadOnlyList<Page>> GetBlogPagesAsync(string authorId);

    /// <summary>
    /// Returns <c>true</c> when the page at <paramref name="pageId"/> is
    /// under the <see cref="PageKind.User"/> (blog) namespace — i.e. its
    /// root is a user page. <c>false</c> for a system page. Used by the Web
    /// tree view (to group "system" vs "blog" roots) and the composer's kind
    /// pre-gate. A missing page returns <c>false</c> (treated as absent).
    /// </summary>
    Task<bool> IsUnderBlogAsync(string pageId);

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
    /// (C3). Standing (ADR 0040, amending §3.7): a <see
    /// cref="PageKind.System"/> page is a GlobalAdmin
    /// (<see cref="Authorization.AccessVia.Admin"/>); a <see
    /// cref="PageKind.User"/> (blog) page is any signed-in actor (they become
    /// the author — <see cref="AccessVia.Owner"/> — the ownership guard keeps
    /// it under their own blog root); a community Moderator has no standing
    /// on either kind. A root-slug collision (two roots sharing a
    /// <see cref="Page.Slug"/> **in the same namespace** — scoped by
    /// <see cref="PageKind"/> + <see cref="Page.AuthorId"/>) is a <see
    /// cref="InvalidOperationException"/> — the <c>(ParentId, Slug)</c>
    /// unique index does **not** prevent it (Postgres treats NULLs as
    /// distinct), so this lane is the authoritative root-slug guard. A
    /// denied actor throws <see cref="UnauthorizedAccessException"/> (the Web
    /// layer's 403) **before** anything is stored.
    /// </summary>
    Task<Page> CreateAsync(
        Page page, string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);

    /// <summary>
    /// Edits an existing <see cref="Page"/> (title / body / audience /
    /// hierarchy fields) in the **caller's** in-flight session (C3). Standing
    /// (ADR 0040, amending §3.7): a <see cref="PageKind.System"/> page is a
    /// GlobalAdmin (<see cref="Authorization.AccessVia.Admin"/>); a <see
    /// cref="PageKind.User"/> (blog) page is the page's <see
    /// cref="Page.AuthorId"/> (<see cref="Authorization.AccessVia.Owner"/>)
    /// or a GlobalAdmin; a community Moderator has no standing on either
    /// kind. A missing page is a <see cref="KeyNotFoundException"/> (the Web
    /// layer's 404); a denied actor is a
    /// <see cref="UnauthorizedAccessException"/> (403).
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
    /// **caller's** in-flight session (C3). Standing (ADR 0040, amending
    /// §3.7): a <see cref="PageKind.System"/> page is a GlobalAdmin
    /// (<see cref="Authorization.AccessVia.Admin"/>); a <see
    /// cref="PageKind.User"/> (blog) page is the author
    /// (<see cref="Authorization.AccessVia.Owner"/>) or a GlobalAdmin; a
    /// community Moderator has no standing on either kind. The **namespace
    /// guard** (a page can only move within its kind's namespace) and the
    /// **blog-ownership guard** (a blog page can only move under the actor's
    /// own blog root) apply on top. Applies the **cycle-guard** (the new
    /// parent is not a descendant of the page) and the **depth-cap** (chain
    /// ≤ <c>MaxDepth</c>, 8); the derived path is rewritten by the single
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
    /// flag out). Standing (ADR 0040, amending §3.7): a <see
    /// cref="PageKind.System"/> page is a GlobalAdmin
    /// (<see cref="Authorization.AccessVia.Admin"/>); a <see
    /// cref="PageKind.User"/> (blog) page is the author
    /// (<see cref="Authorization.AccessVia.Owner"/>) or a GlobalAdmin; a
    /// community Moderator has no standing on either kind.
    /// </summary>
    Task DeleteAsync(
        string pageId, string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);

    /// <summary>
    /// Adds a **user-added translation** of a page in the **caller's**
    /// in-flight session (C3; the ADR 0029 standing carried over). Standing
    /// (ADR 0040, amending §3.7): a GlobalAdmin
    /// (<see cref="Authorization.AccessVia.Admin"/>) or a Translator
    /// (<see cref="Authorization.AccessVia.Admin"/>) — on **either** page
    /// kind; a community Moderator has no standing on either kind. The
    /// <c>(PageId, LanguageCode)</c> unique index (U01) is the add-only
    /// duplicate guard. A missing page is a
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is a
    /// <see cref="UnauthorizedAccessException"/> (403).
    /// </summary>
    Task<PageTranslation> AddTranslationAsync(
        string pageId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);

    /// <summary>
    /// **Updates** an existing user-added translation of a page (ADR 0048).
    /// Standing is the same as <see cref="AddTranslationAsync"/> (a GlobalAdmin
    /// or a Translator); a denied actor is a
    /// <see cref="UnauthorizedAccessException"/> (403); a missing page or row is
    /// a <see cref="KeyNotFoundException"/> (404). One
    /// <c>SaveChangesAsync</c>; a hand-written <see cref="Authorization
    /// .AccessAudit"/> row (action <c>page.translation.update</c>) is stored in
    /// the caller's session.
    /// </summary>
    Task<PageTranslation> UpdateTranslationAsync(
        string pageId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);

    /// <summary>
    /// **Removes** an existing user-added translation of a page (ADR 0048).
    /// Standing is the same as <see cref="AddTranslationAsync"/>; a denied
    /// actor is a <see cref="UnauthorizedAccessException"/> (403); a missing
    /// page or row is a <see cref="KeyNotFoundException"/> (404). One
    /// <c>SaveChangesAsync</c>; a hand-written <see cref="Authorization
    /// .AccessAudit"/> row (action <c>page.translation.remove</c>) is stored in
    /// the caller's session.
    /// </summary>
    Task RemoveTranslationAsync(
        string pageId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);

    /// <summary>
    /// **Resets** a page back to its seeded (first-boot) baseline text (ADR
    /// 0058): overwrites the page's <c>en</c> (source) title/body and its
    /// <c>de</c> / <c>fr</c> / <c>da</c> <see cref="PageTranslation"/> rows
    /// with the code-owned seeded registries (<see cref="Bootstrap
    /// .FirstBootSeeder.EnDefaultPages"/> / <see cref="Bootstrap
    /// .FirstBootSeeder.GuidePages"/> and the matching baseline sets), so a
    /// page can be re-seeded to the latest shipped text after a code
    /// release. Standing is the **same as edit** (ADR 0040 §3.7): a
    /// <see cref="Authorization.AccessVia.Admin"/> (GlobalAdmin) on either
    /// page kind — a community Moderator or a page-author has no reset
    /// standing (reset is a platform-level operation). A page slug with no
    /// seeded baseline (a community-authored page, or a slug the seeded
    /// registries don't carry) is a <see cref="InvalidOperationException"/>
    /// (the form maps this to a validation error, not a 400/500). A denied
    /// actor is a <see cref="UnauthorizedAccessException"/> (403); a missing
    /// or soft-deleted page is a <see cref="KeyNotFoundException"/> (404).
    /// One <c>SaveChangesAsync</c>; a hand-written <see
    /// cref="Authorization.AccessAudit"/> row (action <c>page.reset</c>) is
    /// stored in the caller's session.
    /// </summary>
    Task ResetToSeededAsync(
        string pageId, string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session);
}
