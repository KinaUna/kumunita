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

    // U03: the write lanes (CreateAsync / UpdateAsync / PublishAsync /
    // MoveAsync / DeleteAsync / AddTranslationAsync), each with its C3
    // AccessAudit row (TargetKind = "page"). The standing-matrix gate helpers
    // (CheckCreateStanding / CheckEditStanding / CheckTranslateStanding) and
    // the cycle-guard / depth-cap helpers the write lanes call live on the
    // concrete PageService as public, testable helpers (U02) — they are not
    // part of the Web-facing surface the U04 PageController resolves.
}
