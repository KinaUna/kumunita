using Kumunita.Core.Identity;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Announcements;

/// <summary>
/// Composition service for the <see cref="Announcement"/> bounded context
/// (the "platform announcements" lane — as opposed to a community
/// <see cref="Posts.Post"/>'s audience-restricted lane).
/// <para>
/// Session shape mirrors <see cref="Posts.PostService"/> (invariant C3 —
/// same transaction for writes): reads open their own
/// <c>QuerySession</c> (a plain read; announcements are not
/// audience-restricted, so there is no <c>AccessAudit</c> lane on this
/// bounded context and this service composes only the store seam);
/// writes go through the caller's in-flight <see cref="IDocumentSession"/>
/// (the Web layer's <c>DocumentStore.LightweightSession()</c>) so the
/// write and any other in-session state commit or roll back atomically.
/// </para>
/// <para>
/// <b>The "public vs community" split</b> (see <see cref="AnnouncementScope"/>):
/// a <see cref="AnnouncementScope.Public"/> announcement is visible to
/// <em>every</em> visitor — authenticated or not — and is authorable only
/// by a <see cref="Roles.GlobalAdmin"/>; a
/// <see cref="AnnouncementScope.Community"/> announcement is visible to
/// every signed-in user and is authorable by a GlobalAdmin or a
/// <see cref="Roles.Moderator"/>. <see cref="CreateAsync"/> enforces that
/// split at the Core layer (defense-in-depth — the ASP.NET gate already
/// narrows the author's role, but the Web layer cannot narrow the
/// <em>scope</em> choice by itself: a Moderator could POST a
/// <c>Scope=&"Public"</c> body regardless of the form they were served,
/// so the service is what guarantees the split is real).
/// </para>
/// </summary>
public sealed class AnnouncementService : IAnnouncementService
{
    private readonly IDocumentStore _store;

    public AnnouncementService(IDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// The set of <see cref="Announcement"/>s visible at the caller's
    /// authentication state:
    /// <list type="bullet">
    /// <item><see cref="AnnouncementScope.Public"/> — always, authed or not;</item>
    /// <item><see cref="AnnouncementScope.Community"/> — only when <paramref name="isAuthenticated"/>.</item>
    /// </list>
    /// Sorted by <c>Created</c> descending (latest first). No audit
    /// <see cref="Authorization.AccessAudit"/> row (announcements are not
    /// audience-restricted, so there's no per-item decision to log — the
    /// coarse role gate is the whole decision, and it's a view-model
    /// filter, not a call into <c>IAuthorizationService</c>).
    /// </summary>
    public async Task<IReadOnlyList<Announcement>> ListVisibleAsync(bool isAuthenticated)
    {
        await using var session = _store.QuerySession();
        return await session
            .Query<Announcement>()
            .Where(a => a.Scope == AnnouncementScope.Public ||
                        (isAuthenticated && a.Scope == AnnouncementScope.Community))
            .OrderByDescending(a => a.Created)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an <see cref="Announcement"/> in the <b>caller's</b> in-flight
    /// session (invariant C3 — the same-transaction lane, see
    /// <see cref="Posts.PostService.CreatePostAsync"/> for the shape).
    /// Enforces the scope-vs-role split:
    /// <list type="bullet">
    /// <item><see cref="AnnouncementScope.Public"/> — the author must hold <see cref="Roles.GlobalAdmin"/>;</item>
    /// <item><see cref="AnnouncementScope.Community"/> — the author must hold <see cref="Roles.GlobalAdmin"/>
    /// <b>or</b> <see cref="Roles.Moderator"/>.</item>
    /// </list>
    /// A denied split is a hard <see cref="UnauthorizedAccessException"/>
    /// (the Web layer maps that to a 403) — NOT a silent no-op, and NOT
    /// the <c>AccessAudit</c> decision lane (announcements are not
    /// audience-restricted; the audit lane is for per-user decisions,
    /// which this flat split is not). <paramref name="authorRoles"/> is
    /// the caller's role claim set (the Web layer's principal, not a DB
    /// read — same claim-set-as-principal seam the ASP.NET gate reads).
    /// </summary>
    public async Task<Announcement> CreateAsync(
        Announcement announcement,
        string actorId,
        IReadOnlySet<string> authorRoles,
        IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An authoring actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(authorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var hasGlobalAdmin = authorRoles.Contains(Roles.GlobalAdmin);
        var hasModerator   = authorRoles.Contains(Roles.Moderator);

        switch (announcement.Scope)
        {
            case AnnouncementScope.Public when !hasGlobalAdmin:
                throw new UnauthorizedAccessException("Only a GlobalAdmin may create a public-scope announcement.");

            case AnnouncementScope.Community when !hasGlobalAdmin && !hasModerator:
                throw new UnauthorizedAccessException("Only a GlobalAdmin or Moderator may create a community-scope announcement.");

            default:
                break;
        }

        if (string.IsNullOrEmpty(announcement.Id))
            announcement.Id = Guid.NewGuid().ToString("N");
        announcement.AuthorId = actorId;
        announcement.Created  = DateTimeOffset.UtcNow;

        session.Store(announcement);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return announcement;
    }

    /// <summary>
    /// Deletes an <see cref="Announcement"/> in the <b>caller's</b> in-flight
    /// session (invariant C3). A hard delete (no soft-hidden state —
    /// announcements are a flat public surface, not audience-restricted
    /// content, so there's no <see cref="Posts.PostStatus"/>-shaped
    /// surface to preserve). The Web layer's
    /// <c>[Authorize(Roles = GlobalAdmin)]</c> gate is the sole role
    /// check (a single valid author role means there's no split to
    /// re-check at the Core layer the way <see cref="CreateAsync"/>'s
    /// scope-vs-role split does). A missing id is a
    /// <see cref="KeyNotFoundException"/> (the Web layer maps that to a 404).
    /// </summary>
    public async Task DeleteAsync(string announcementId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(announcementId))
            throw new ArgumentException("An announcement id is required.", nameof(announcementId));
        ArgumentNullException.ThrowIfNull(session);

        var announcement = await session.LoadAsync<Announcement>(announcementId).ConfigureAwait(false);
        if (announcement is null)
            throw new KeyNotFoundException($"Announcement '{announcementId}' was not found in the session; nothing to delete.");

        session.Delete(announcement);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }
}
