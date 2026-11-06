using Kumunita.Core.Notifications;
using Kumunita.Core.Translations;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// Shared helpers for the UserInfo module's service cluster (C07, ADR 0286).
/// Each helper satisfies the part-vs-whole test: ≥ 2 callers across sibling impls
/// (or the access core + a sibling). Instance class because the notification-
/// emitter helpers need the <c>IServiceProvider</c> seam for lazy
/// <c>INotificationEmitter</c> resolution.
/// </summary>
internal sealed class UserInfoStore
{
    public UserInfoStore(IDocumentStore store, IServiceProvider? services = null)
    {
        _store = store;
        _services = services;
    }

    private readonly IDocumentStore _store;
    private readonly IServiceProvider? _services;

    internal static void NameOrDescriptionRequired(string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A name/description translation needs at least a name or a description.", nameof(name));
    }

    /// <summary>
    /// The child's <b>active</b> guardian id set (the GU community-approval
    /// lane's notification fan-out): the <c>GuardianId</c>s of every
    /// <see cref="GuardianLinkStatus.Active"/> row where
    /// <see cref="GuardianLink.ChildId"/> = <paramref name="childId"/>.
    /// An unsupervised child (no active row) returns an empty set — the
    /// caller falls back to the child-facing lane (the ADR 0083
    /// <c>group.invite</c> / <c>group.added</c> shape).
    /// </summary>
    internal async Task<IReadOnlyCollection<string>> ActiveGuardianIdsAsync(
        IDocumentSession session, string childId)
    {
        var links = await session.Query<GuardianLink>()
            .Where(l => l.ChildId == childId && l.Status == GuardianLinkStatus.Active)
            .ToListAsync()
            .ConfigureAwait(false);
        return links.Select(l => l.GuardianId).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The GU standing gate (G·2 live / G·3 deny-by-default): an <b>active</b>
    /// <see cref="GuardianLink"/> with <see cref="GuardianLink.GuardianId"/>
    /// equal to <paramref name="guardianId"/> and <see cref="GuardianLink
    /// .ChildId"/> equal to <paramref name="childId"/> must exist — else
    /// <see cref="UnauthorizedAccessException"/> (the Web's 404). The row is
    /// resolved **live** off <see cref="GuardianLinkStatus"/> (the service is
    /// the resolver — the POCO carries state, not an <c>IsActive</c> boolean).
    /// </summary>
    /// <summary>
    /// The <b>child's display name</b> for a GU community-approval lane
    /// notification body (a curation fact the parent already sees in their
    /// <c>/me/children</c> list — the <see cref="ChildAccountItem"/>
    /// precedent — not a G·1-hiding content read): the child's
    /// <c>Profile.DisplayName</c> when present, else the raw
    /// <c>SubjectId</c> (the fail-safe the list already uses).
    /// </summary>
    internal async Task<string> ChildDisplayNameAsync(string childId)
    {
        await using var s = _store.QuerySession();
        var profile = await s.LoadAsync<Profile>(childId).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(profile?.DisplayName)
            ? childId
            : profile!.DisplayName;
    }

    /// <summary>
    /// The GU community-approval lane's <b>guardian fan-out</b> emission: for
    /// every active guardian of <paramref name="childId"/>, _store a
    /// <see cref="Notifications.Notification"/> inbox row + (best-effort)
    /// staged email with <see cref="Notifications.Notification.LinkPath"/>
    /// = <c>/me/children/{childId}</c> (the manage-child page, where the
    /// pending list + approve/reject buttons live) and
    /// <c>AcceptPath</c>/<c>DeclinePath</c> = the same (the inbox button
    /// + the email link both deep-link to the page; the child id is
    /// sufficient — the target pair is in the body / the page's list).
    /// <para>
    /// The idempotency key is per-<b>(guardian, target, child)</b> — the
    /// <see cref="Notifications.NotificationService"/> dedups on
    /// <c>IdempotencyKey</c>, so a re-invite with the same
    /// (guardian, child, target) is a no-op. The <c>body</c> is the
    /// UGC snippet the <see cref="Notifications.NotificationService
    /// .EmitAsync"/> template appends (the child's display name —
    /// a curation fact the parent already sees in their <c>/me/children</c>
    /// list, not content the G·1 rule hides from a parent — the
    /// <see cref="ChildAccountItem"/> precedent).
    /// </para>
    /// <para>
    /// When <paramref name="notificationService"/> is null
    /// (the 181 pre-ADR 0083 direct-construction test harnesses
    /// that build <c>UserInfoService(_store)</c> positionally pass
    /// <c>_services = null</c>), the emission is a silent no-op —
    /// the domain write still commits (C3).
    /// </para>
    /// </summary>
    internal async Task EmitGuardianNotificationAsync(
        IDocumentSession session,
        string childId,
        string guardianId,
        string kind,
        string targetId,
        string body)
    {
        var ns = _services?.GetService<INotificationEmitter>();
        if (ns is null) return;

        var linkPath = $"/me/children/{childId}";
        await ns.EmitAsync(
            session,
            guardianId,
            kind,
            $"notification:{kind}:{targetId}:{childId}:{guardianId}",
            body,
            linkPath: linkPath,
            acceptPath: linkPath,
            declinePath: linkPath,
            ct: System.Threading.CancellationToken.None)
            .ConfigureAwait(false);
    }

}
