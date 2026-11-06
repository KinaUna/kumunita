using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Query;
using Kumunita.Core.Translations;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Announcements;

/// <summary>
/// The <c>/announcements</c> resident-comment surface — the
/// ADR 0101 comment CRUD + enablement toggle + count reads.
/// A partial class of <see cref="AnnouncementService"/>
/// extracted so the main file stays under the 600 <c>cs_file</c>
/// ceiling (C3-04).
/// </summary>
public partial class AnnouncementService
{
    public async Task<bool> AreAnnouncementCommentsEnabledAsync()
    {
        using var session = _store.QuerySession();
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None)
            .ConfigureAwait(false);
        // The <c>true</c> floor: a missing singleton reads as "comments on"
        // (the NotifyAdminsOnSignup precedent).
        return settings is null || settings.AnnouncementCommentsEnabled;
    }

    public async Task SetAnnouncementCommentsEnabledAsync(bool enabled, string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to change the announcement-comments gate.");

        await using var session = _store.OpenSession(new SessionOptions());
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None)
            .ConfigureAwait(false);
        if (settings is null)
            settings = new LocaleSettings { AnnouncementCommentsEnabled = enabled };
        else
            settings.AnnouncementCommentsEnabled = enabled;

        // The singleton-toggle audit row (the signup.set-open / timezone.set-default
        // shape — the <c>true</c> floor, ADR 0004 §B.1 additive field).
        session.Store(settings);
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "announcementcomments.set-enabled",
            TargetKind = "announcementcomments",
            TargetId = "announcementcomments",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AnnouncementComment>> GetAnnouncementCommentsAsync(
        string announcementId, string? actorId, IReadOnlySet<string> actorRoles)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (string.IsNullOrEmpty(announcementId))
            throw new KeyNotFoundException("An announcement id is required.");

        // ADR 0101 — signed-in only: a visitor (no subject id) is refused the
        // comment list outright (the 403 shape — a comment surface, not a
        // content read, so the Web maps this to Forbid).
        if (string.IsNullOrWhiteSpace(actorId))
            throw new UnauthorizedAccessException("Comments are visible to signed-in residents only.");

        using var session = _store.QuerySession();
        var announcement = await session
            .LoadAsync<Announcement>(announcementId, CancellationToken.None)
            .ConfigureAwait(false);
        if (announcement is null)
            throw new KeyNotFoundException($"Announcement '{announcementId}' was not found.");

        // The comment is visible only under the announcement's own flat scope
        // split (the GetAsync predicate) — a community-targeted announcement's
        // comments go to that community's members / moderators / a GlobalAdmin
        // only; a public one to any signed-in user. A non-visible announcement
        // is a 404 (non-leaky, the announcement lane's posture) — the Web maps
        // KeyNotFound to NotFound.
        var resolver = await ResolveReadVisibilityAsync(actorId, actorRoles).ConfigureAwait(false);
        if (announcement.IsDraft || !IsAnnouncementVisible(announcement, resolver))
            throw new KeyNotFoundException($"Announcement '{announcementId}' was not found.");

        return (await session
            .Query<AnnouncementComment>()
            .Where(c => c.AnnouncementId == announcementId)
            .OrderBy(c => c.Created)
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false)).ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> CountAnnouncementCommentsAsync(
        IReadOnlyCollection<string> announcementIds)
    {
        ArgumentNullException.ThrowIfNull(announcementIds);
        if (announcementIds.Count == 0)
            return new Dictionary<string, int>();

        using var session = _store.QuerySession();
        // TODO (ADR 0248): a list page holds at most PageSize announcements, so one projected
        // column is fine; move the grouping into the query if a page ever carries many comments.
        var owners = await session
            .Query<AnnouncementComment>()
            .Where(c => announcementIds.Contains(c.AnnouncementId))
            .Select(c => c.AnnouncementId)
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false);

        return owners
            .GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }

    public async Task<AnnouncementComment> CreateAnnouncementCommentAsync(
        string announcementId,
        string actorId,
        IReadOnlySet<string> actorRoles,
        string body,
        string? languageCode,
        IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrEmpty(announcementId))
            throw new KeyNotFoundException("An announcement id is required.");
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A comment body is required.", nameof(body));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to comment on an announcement.");

        // ADR 0101 — signed-in only (enforced above by the actorId check) and
        // admin-toggle-gated.
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None)
            .ConfigureAwait(false);
        if (settings is not null && !settings.AnnouncementCommentsEnabled)
            throw new UnauthorizedAccessException("Commenting on announcements is currently disabled.");

        // The announcement must exist and be visible to the actor under its
        // flat scope split (a missing / not-visible id is a 404, non-leaky).
        var announcement = await session
            .LoadAsync<Announcement>(announcementId, CancellationToken.None)
            .ConfigureAwait(false);
        if (announcement is null)
            throw new KeyNotFoundException($"Announcement '{announcementId}' was not found; nothing to comment on.");
        var resolver = await ResolveReadVisibilityAsync(actorId, actorRoles).ConfigureAwait(false);
        if (announcement.IsDraft || !IsAnnouncementVisible(announcement, resolver))
            throw new KeyNotFoundException($"Announcement '{announcementId}' was not found; nothing to comment on.");

        var now = DateTimeOffset.UtcNow;
        var comment = new AnnouncementComment
        {
            Id = Guid.NewGuid().ToString("N"),
            AnnouncementId = announcementId,
            AuthorId = actorId,
            Body = body,
            Created = now,
            LanguageCode = await ResolveLanguageCodeAsync(languageCode, session).ConfigureAwait(false) // ADR 0018 — the authored-in tag (instance-default floor).
        };

        // Audit row (the ADR 0029 / ADR 0100 write-lane precedent — a hand-written
        // row; the C3 atomic commit).
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "announcementcomment.create",
            TargetKind = "announcement",
            TargetId = announcementId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(comment);
        session.Store(audit);
        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return comment;
    }

    public async Task<AnnouncementComment> DeleteAnnouncementCommentAsync(
        string announcementId,
        string commentId,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrEmpty(announcementId))
            throw new KeyNotFoundException("An announcement id is required.");
        if (string.IsNullOrEmpty(commentId))
            throw new KeyNotFoundException("A comment id is required.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to delete a comment.");

        var comment = await session
            .LoadAsync<AnnouncementComment>(commentId, CancellationToken.None)
            .ConfigureAwait(false);
        if (comment is null)
            throw new KeyNotFoundException($"Comment '{commentId}' was not found in the session; nothing to delete.");
        if (!string.Equals(comment.AnnouncementId, announcementId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Comment '{commentId}' is not on announcement '{announcementId}'; nothing to delete.");

        // Standing (ADR 0016 / ADR 0024 shape): author-only. A non-author is
        // refused before any write (the ADR 0016 reply-delete precedent — no
        // moderator / GlobalAdmin branch on a comment's own delete).
        if (!string.Equals(comment.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Actor is not the author of comment '{commentId}'.");

        comment.DeletedAt = DateTimeOffset.UtcNow; // ADR 0024 — the soft-delete stamp.
        var now = comment.DeletedAt!.Value;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "announcementcomment.delete",
            TargetKind = "announcement",
            TargetId = announcementId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(comment);
        session.Store(audit);
        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return comment;
    }
}
