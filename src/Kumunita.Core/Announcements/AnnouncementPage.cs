namespace Kumunita.Core.Announcements;

/// <summary>
/// The paged <see cref="Announcement"/> feed (ADR 0090 D6, M7 U01 — the
/// design doc §7.6 lock): one page of the <see cref="AnnouncementService.ListVisiblePagedAsync"/>
/// read plus its sole paging signal.
/// <para>
/// <see cref="HasMore"/> (D1) is the **only** paging signal —
/// <c>true</c> iff the page's candidate set filled the page
/// (<c>pageCount == PageSize</c>, <c>PageSize = 30</c> — the D4 shape);
/// the Web computes <c>HasNextPage</c> from it alone and **never divides a
/// total** (there is no <c>Total</c> field — D2 never applies here:
/// announcements are not audience-restricted, so there is no pre-decision
/// candidate count to report). There is **no**
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row (announcements
/// have no audit lane — the <see cref="AnnouncementService.ListVisibleAsync"/>
/// pin; C-M7·1 vacuously satisfied).
/// </para>
/// </summary>
public sealed record AnnouncementPage(
    IReadOnlyList<Announcement> Items,
    bool HasMore);
