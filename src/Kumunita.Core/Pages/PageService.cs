using Marten;

namespace Kumunita.Core.Pages;

/// <summary>
/// The <c>/pages</c> bounded-context's store-composing service (ADR 0039;
/// the Pages lane <c>PG</c>). Mirrors the <see cref="Announcements
/// .AnnouncementService"/> shape: a single implementation of
/// <see cref="IPageService"/> that composes the host-registered
/// <see cref="IDocumentStore"/> (plus the frozen
/// <see cref="Authorization.IAuthorizationService"/> /
/// <see cref="IUserInfoService"/> seams as the write lanes land in U03), kept
/// behind the interface so the Web-side consumer (the U04
/// <c>PageController</c>) can be tested with an NSubstitute double instead of
/// a live Postgres (the <see cref="IAnnouncementService"/> convention).
/// <para>
/// **U01 (this unit) is the DI seam only — no read or write methods yet.**
/// The <see cref="IDocumentStore"/> is resolved now (the store-injection
/// shape is the load-bearing part U02/U03 build on); the read lanes
/// (<c>GetByPathAsync</c> / <c>GetTreeAsync</c> / …) land in U02 and the
/// write lanes (<c>CreateAsync</c> / <c>UpdateAsync</c> / …) in U03, each
/// re-checking standing server-side (the C3 pattern, ADR 0006) and writing
/// its <see cref="Authorization.AccessAudit"/> row in the caller's session.
/// </para>
/// </summary>
public sealed class PageService : IPageService
{
    private readonly IDocumentStore _store;

    public PageService(IDocumentStore store)
    {
        _store = store;
    }

    // U02: the read lanes (GetByPathAsync / GetTreeAsync /
    // GetByMountPointAsync / GetTranslationsAsync / GetBySlugUnderParentAsync)
    // + the PageToAuditableResource adapter + the CanSeeAsync(Read) tree
    // filter.
    // U03: the write lanes (CreateAsync / UpdateAsync / PublishAsync /
    // MoveAsync / DeleteAsync / AddTranslationAsync), each with its C3
    // AccessAudit row (TargetKind = "page").
}
