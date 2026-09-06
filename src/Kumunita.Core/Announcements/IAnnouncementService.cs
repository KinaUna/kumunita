using Kumunita.Core.Identity;
using Marten;

namespace Kumunita.Core.Announcements;

/// <summary>
/// The <c>/announcements</c> bounded-context's service seam (M4, the "platform
/// announcements" lane). The public surface of <see cref="AnnouncementService"/>:
/// the <see cref="ListVisibleAsync"/> / <see cref="CreateAsync"/> /
/// <see cref="DeleteAsync"/> triad.
/// <para>
/// Kept behind an interface so the Web-side consumer (the
/// <see cref="Kumunita.Web.Controllers.AnnouncementController"/>) can be tested
/// without a live Postgres: the real implementation opens
/// <c>IQuerySession</c>/<c>LightweightSession</c> against the store, and a test
/// double (NSubstitute) returns a canned list / throws the seam's contract
/// exception (<see cref="UnauthorizedAccessException"/> on a denied scope-vs-role
/// split, <see cref="KeyNotFoundException"/> on a missing delete id). This mirrors
/// the repo's existing service-seam convention (<see cref="IUserInfoService"/>,
/// <see cref="IAuthorizationService"/>, <see cref="IEmailDeadLetterCounter"/> —
/// the last one being the closest analog: a store-composing service that a Web
/// controller test must substitute).
/// </para>
/// </summary>
public interface IAnnouncementService
{
    /// <summary>
    /// The set of <see cref="Announcement"/>s visible at the caller's
    /// authentication state: <see cref="AnnouncementScope.Public"/> always,
    /// <see cref="AnnouncementScope.Community"/> when <paramref name="isAuthenticated"/>.
    /// Sorted by <c>Created</c> descending (latest first); no
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row (announcements
    /// are not audience-restricted). See <see cref="AnnouncementService.ListVisibleAsync"/>
    /// for the full contract.
    /// </summary>
    Task<IReadOnlyList<Announcement>> ListVisibleAsync(bool isAuthenticated);

    /// <summary>
    /// Creates an <see cref="Announcement"/> in the <b>caller's</b> in-flight
    /// session (invariant C3). Enforces the scope-vs-role split — a
    /// <see cref="Roles.GlobalAdmin"/> may author either scope; a
    /// <see cref="Roles.Moderator"/> may author
    /// <see cref="AnnouncementScope.Community"/> only. A denied split is a hard
    /// <see cref="UnauthorizedAccessException"/> (the Web layer maps that to a
    /// 403). See <see cref="AnnouncementService.CreateAsync"/> for the full
    /// contract.
    /// </summary>
    Task<Announcement> CreateAsync(
        Announcement announcement,
        string actorId,
        IReadOnlySet<string> authorRoles,
        IDocumentSession session);

    /// <summary>
    /// Deletes an <see cref="Announcement"/> in the <b>caller's</b> in-flight
    /// session (invariant C3). Hard delete (no soft-hidden state). A missing id
    /// is a <see cref="KeyNotFoundException"/> (the Web layer maps that to a 404).
    /// See <see cref="AnnouncementService.DeleteAsync"/> for the full contract.
    /// </summary>
    Task DeleteAsync(string announcementId, IDocumentSession session);
}
