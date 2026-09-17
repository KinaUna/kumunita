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
    // U02: the read lanes (GetByPathAsync / GetTreeAsync / GetByMountPointAsync
    // / GetTranslationsAsync / GetBySlugUnderParentAsync) + the
    // PageToAuditableResource adapter + the CanSeeAsync(Read) tree filter.
    // U03: the write lanes (CreateAsync / UpdateAsync / PublishAsync /
    // MoveAsync / DeleteAsync / AddTranslationAsync), each with its C3
    // AccessAudit row (TargetKind = "page").
}
