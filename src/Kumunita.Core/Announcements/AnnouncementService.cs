using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
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
/// <see cref="Roles.Moderator"/>; it may also <em>target</em> a specific
/// community (a <c>CommunityId</c>), which then makes it visible only to
/// that community's members or moderators and a <see cref="Roles.GlobalAdmin"/>
/// and is authorable by that community's moderator or a
/// <see cref="Roles.GlobalAdmin"/>. <see cref="CreateAsync"/> enforces that
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
    private readonly IUserInfoService _userInfo;

    public AnnouncementService(IDocumentStore store, IUserInfoService userInfo)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    /// <summary>
    /// The set of <see cref="Announcement"/>s visible at the caller's
    /// authentication state:
    /// <list type="bullet">
    /// <item><see cref="AnnouncementScope.Public"/> — always, authed or not;</item>
    /// <item><see cref="AnnouncementScope.Community"/> — only when <paramref name="roles"/>.</item>
    /// </list>
    /// Sorted by <c>Created</c> descending (latest first). No audit
    /// <see cref="Authorization.AccessAudit"/> row (announcements are not
    /// audience-restricted, so there's no per-item decision to log — the
    /// coarse role gate is the whole decision, and it's a view-model
    /// filter, not a call into <c>IAuthorizationService</c>).
    /// </summary>
    public async Task<IReadOnlyList<Announcement>> ListVisibleAsync(string? actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var (authed, admin, communities) = await ResolveReadVisibilityAsync(actorId, roles).ConfigureAwait(false);

        await using var session = _store.QuerySession();
        return await session
            .Query<Announcement>()
            .Where(a => (a.CommunityId == null &&
                         (a.Scope == AnnouncementScope.Public ||
                          (authed && a.Scope == AnnouncementScope.Community)))
                      || (a.CommunityId != null &&
                         (admin || communities.Contains(a.CommunityId!))))
            .OrderByDescending(a => a.Created)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The single announcement to render as a site-wide banner:
    /// the most-recently-created <see cref="Announcement"/> with
    /// <see cref="Announcement.Pinned" /> true that passes the caller's
    /// authentication state (the same gate as <see cref="ListVisibleAsync"/>:
    /// <see cref="AnnouncementScope.Public" /> always;
    /// <see cref="AnnouncementScope.Community" /> only when
    /// <paramref name="roles"/>). Returns null when no pinned
    /// announcement passes (the Web layer skips the banner in that case).
    /// No <c>AccessAudit</c> row (same reasoning as
    /// <see cref="ListVisibleAsync"/> — announcements are not
    /// audience-restricted; the scope-vs-role split is the whole decision).
    /// </summary>
    public async Task<Announcement?> PinnedAsync(string? actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var (authed, admin, communities) = await ResolveReadVisibilityAsync(actorId, roles).ConfigureAwait(false);

        await using var session = _store.QuerySession();
        return await session
            .Query<Announcement>()
            .Where(a => a.Pinned == true &&
                        (a.CommunityId == null &&
                        ((a.Scope == AnnouncementScope.Public ||
                          (authed && a.Scope == AnnouncementScope.Community)))
                     || (a.CommunityId != null &&
                        (admin || communities.Contains(a.CommunityId!)))))
            .OrderByDescending(a => a.Created)
            .FirstOrDefaultAsync()
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

        await EnsureWritePermissionAsync(announcement, authorRoles).ConfigureAwait(false);

        if (string.IsNullOrEmpty(announcement.Id))
            announcement.Id = Guid.NewGuid().ToString("N");
        announcement.AuthorId = actorId;
        announcement.Created  = DateTimeOffset.UtcNow;

        session.Store(announcement);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return announcement;
    }

    /// <summary>
    /// Edits an existing <see cref="Announcement"/> in the <b>caller's</b>
    /// in-flight session (invariant C3, mirroring <see cref="CreateAsync"/>
    /// and <see cref="DeleteAsync"/>'s write lane). Applies the same
    /// scope-vs-role split as <see cref="CreateAsync"/>, but against the
    /// <see cref="Announcement.Scope"/> of the edited document (the new
    /// scope, if the caller is changing it — not the previously stored one,
    /// since a scope change is itself the edit in question):
    /// <list type="bullet">
    /// <item><see cref="AnnouncementScope.Public"/> — the actor must hold <see cref="Roles.GlobalAdmin"/>;</item>
    /// <item><see cref="AnnouncementScope.Community"/> — the actor must hold <see cref="Roles.GlobalAdmin"/>
    /// <b>or</b> <see cref="Roles.Moderator"/>.</item>
    /// </list>
    /// A denied split is a hard <see cref="UnauthorizedAccessException"/>
    /// (the Web layer maps that to a 403); a missing id is a
    /// <see cref="KeyNotFoundException"/> (the Web layer maps that to a 404).
    /// <see cref="Announcement.AuthorId"/> and <see cref="Announcement.Created"/>
    /// are deliberately <b>not</b> reassigned here (unlike
    /// <see cref="CreateAsync"/>, which mints a brand-new doc): the author of
    /// record is whoever created it, not whoever last edited it, and
    /// <see cref="Announcement.Modified"/> is stamped — <see cref="DateTimeOffset.UtcNow"/>
    /// — only when at least one of Title/Body/Scope/Pinned actually changed,
    /// so a no-op re-save of an unchanged doc does not bump the stamp.
    /// </summary>
    public async Task<Announcement> UpdateAsync(
        Announcement updated,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(updated);
        if (string.IsNullOrEmpty(updated.Id)) throw new ArgumentException("An announcement id is required.", nameof(updated.Id));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        await EnsureWritePermissionAsync(updated, actorRoles).ConfigureAwait(false);

        var existing = await session.LoadAsync<Announcement>(updated.Id).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Announcement '{updated.Id}' was not found in the session; nothing to edit.");

        var changed = existing.Title != updated.Title
            || existing.Body != updated.Body
            || existing.Scope != updated.Scope
            || existing.Pinned != updated.Pinned
            || existing.CommunityId != updated.CommunityId;

        existing.Title = updated.Title;
        existing.Body  = updated.Body;
        existing.Scope = updated.Scope;
        existing.Pinned = updated.Pinned;
        existing.CommunityId = updated.CommunityId;
        if (changed)
            existing.Modified = DateTimeOffset.UtcNow;

        session.Store(existing);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return existing;
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
    /// <summary>
    /// Resolves the actor's read-visibility for announcements: whether they are signed in
    /// (<c>authed</c>), a GlobalAdmin (<c>admin</c> — sees every target), and the set of
    /// communities in which they may see a targeted announcement (their membership set from
    /// <see cref="IUserInfoService.GetCommunityIdsAsync"/> unioned with the communities they
    /// moderate, read from their <c>moderator:{id}</c> standing claims). An anonymous or
    /// GlobalAdmin caller gets an empty community set (the admin flag already authorizes all
    /// targets for them, so the list is irrelevant).
    /// </summary>
    private async Task<(bool authed, bool admin, IReadOnlyList<string> communities)> ResolveReadVisibilityAsync(
        string? actorId,
        IReadOnlySet<string> roles)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return (false, false, new List<string>());

        var admin = roles.Contains(Roles.GlobalAdmin);
        if (admin)
            return (true, admin, new List<string>());

        var communities = new List<string>();
        foreach (var role in roles)
        {
            if (role.StartsWith("moderator:", StringComparison.Ordinal))
                communities.Add(role[Roles.ModeratorComponent("").Length..]);
        }

        var membership = await _userInfo.GetCommunityIdsAsync(actorId).ConfigureAwait(false);
        if (membership is not null)
        {
            foreach (var communityId in membership)
                communities.Add(communityId);
        }

        return (true, false, communities);
    }

    /// <summary>
    /// The write gate shared by <see cref="CreateAsync"/> and <see cref="UpdateAsync"/>. A
    /// <see cref="AnnouncementScope.Public"/> announcement is always platform-wide so it cannot
    /// carry a <c>CommunityId</c>; a <see cref="AnnouncementScope.Community"/> announcement with
    /// no <c>CommunityId</c> is the flat "all residents" target (a GlobalAdmin or Moderator);
    /// one with a <c>CommunityId</c> targets that community and requires a GlobalAdmin or the
    /// <c>moderator:{CommunityId}</c> standing claim, and the target must name a real component.
    /// A denied author is <see cref="UnauthorizedAccessException"/> (mapped to a 403); an invalid
    /// shape or unknown target is an <see cref="ArgumentException"/> (mapped to a 400).
    /// </summary>
    private async Task EnsureWritePermissionAsync(Announcement announcement, IReadOnlySet<string> authorRoles)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        ArgumentNullException.ThrowIfNull(authorRoles);

        var hasGlobalAdmin = authorRoles.Contains(Roles.GlobalAdmin);
        var hasModerator   = authorRoles.Contains(Roles.Moderator);

        if (announcement.CommunityId is not null && announcement.Scope == AnnouncementScope.Public)
            throw new ArgumentException(
                "A public-scope announcement cannot target a community; clear CommunityId or use the Community scope.",
                nameof(announcement));

        if (announcement.CommunityId is null)
        {
            switch (announcement.Scope)
            {
                case AnnouncementScope.Public when !hasGlobalAdmin:
                    throw new UnauthorizedAccessException("Only a GlobalAdmin may create a public-scope announcement.");

                case AnnouncementScope.Community when !hasGlobalAdmin && !hasModerator:
                    throw new UnauthorizedAccessException("Only a GlobalAdmin or Moderator may create a community-scope announcement.");

                default:
                    break;
            }
            return;
        }

        var target = announcement.CommunityId!;
        var moderatesTarget = authorRoles.Contains(Roles.ModeratorComponent(target));
        if (!hasGlobalAdmin && !moderatesTarget)
            throw new UnauthorizedAccessException(
                $"Only a GlobalAdmin or a moderator of community '{target}' may target that community.");

        var components = await _userInfo.GetComponentsAsync(enabledOnly: true).ConfigureAwait(false);
        if (components is null || !components.Any(c => c.Id == target))
            throw new ArgumentException(
                $"Unknown community '{target}' for the announcement target.",
                nameof(announcement));
    }
}
