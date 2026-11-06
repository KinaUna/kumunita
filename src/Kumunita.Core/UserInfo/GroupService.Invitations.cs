using Kumunita.Core.Notifications;
using Kumunita.Core.Translations;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// The invitation lane — the accept/decline/cancel group-invitation
/// actions + the pending-invitation reads (ADR 0007 group-management,
/// ADR 0008 member-self-leave). A partial class of
/// <see cref="GroupService"/> extracted so the main file stays under
/// the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class GroupService
{
    /// <inheritdoc />
    public async Task AcceptGroupInvitationAsync(string groupId, string actorId)
    {
        // Self-lane (C-M2b·2): the actor resolves their OWN row — verified in
        // this method, not only by the Web gate. In the same session the row
        // moves Pending → Accepted and the GroupMembership row is upserted
        // (live on the next GetGroupIdsAsync / GetGroupsForUserAsync — C4
        // carried). One SaveChangesAsync (invariant C3).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // GU gate (ADR 0028 §C / G·2): a supervised child — one with an active
        // GuardianLink — may NOT self-accept. Read-only (no mutation); the
        // approval must come from their guardian via ApproveGroupInvitationAsync.
        var supervised = await session.Query<GuardianLink>()
            .Where(l => l.ChildId == actorId && l.Status == GuardianLinkStatus.Active)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (supervised is not null)
            throw new InvalidOperationException(
                $"Account {actorId} is supervised; a group invitation must be approved by their guardian (see ApproveGroupInvitationAsync).");

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        var row = await session.Query<GroupInvitation>()
            .Where(i => i.GroupId == groupId && i.UserId == actorId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        // C-M2b·2: a row that isn't the actor's own is an invalid self-lane
        // call (the Web's 404 gate is the first wall; this is the Core's).
        if (row is null || row.UserId != actorId)
            throw new InvalidOperationException(
                $"No group invitation for {actorId} in group {groupId}");

        // C-M2b·3: only Pending resolves; an already-resolved row (Accepted /
        // Declined / Cancelled) is an invalid transition.
        if (row.Status != InvitationStatus.Pending)
            throw new InvalidOperationException(
                $"Invitation {row.Id} is already {row.Status}; only a Pending invitation can be accepted.");

        row.Status = InvitationStatus.Accepted;
        row.ResolvedAt = now;
        row.ResolvedBy = actorId;
        session.Store(row);

        // The membership lands here — the exact AddGroupMemberAsync upsert
        // shape (strong consistency, invariant C4).
        var membership = await session.Query<GroupMembership>()
            .Where(m => m.GroupId == groupId && m.UserId == actorId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (membership is null)
        {
            membership = new GroupMembership
            {
                Id = Guid.NewGuid().ToString("N"),
                GroupId = groupId,
                UserId = actorId,
                AddedBy = actorId,
                At = now
            };
        }
        else
        {
            membership.AddedBy = actorId;
            membership.At = now;
        }

        session.Store(membership);

        // Self-lane audit: the invitee's own standing (not the owner's, not
        // an admin's) — Via Owner with the invitee as all three identities.
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "group.invite.accept",
            TargetKind = "group",
            TargetId = groupId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    /// <inheritdoc />
    public async Task DeclineGroupInvitationAsync(string groupId, string actorId)
    {
        // Self-lane (C-M2b·2) — the same gate + state check as
        // AcceptGroupInvitationAsync, but no GroupMembership row is touched.
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var row = await session.Query<GroupInvitation>()
            .Where(i => i.GroupId == groupId && i.UserId == actorId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null || row.UserId != actorId)
            throw new InvalidOperationException(
                $"No group invitation for {actorId} in group {groupId}");

        if (row.Status != InvitationStatus.Pending)
            throw new InvalidOperationException(
                $"Invitation {row.Id} is already {row.Status}; only a Pending invitation can be declined.");

        row.Status = InvitationStatus.Declined;
        row.ResolvedAt = now;
        row.ResolvedBy = actorId;
        session.Store(row);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "group.invite.decline",
            TargetKind = "group",
            TargetId = groupId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    /// <inheritdoc />
    public async Task CancelGroupInvitationAsync(string groupId, string userId, string cancelledBy)
    {
        // Owner/admin lane (C-M2b·1): the row must exist and be Pending
        // (C-M2b·3); the Via derivation is exactly the group add/remove rule
        // (cancelledBy == OwnerId ⇒ Owner, else Admin). One SaveChangesAsync
        // (invariant C3).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        var row = await session.Query<GroupInvitation>()
            .Where(i => i.GroupId == groupId && i.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException(
                $"No group invitation for {userId} in group {groupId} to cancel.");

        if (row.Status != InvitationStatus.Pending)
            throw new InvalidOperationException(
                $"Invitation {row.Id} is already {row.Status}; only a Pending invitation can be cancelled.");

        row.Status = InvitationStatus.Cancelled;
        row.ResolvedAt = now;
        row.ResolvedBy = cancelledBy;
        session.Store(row);

        var via = cancelledBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? group.OwnerId : cancelledBy;

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = cancelledBy,
            EffectivePrincipalId = effective,
            Action = "group.invite.cancel",
            TargetKind = "group",
            TargetId = groupId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupInvitation>> GetPendingInvitationsForUserAsync(string userId)
    {
        // The invitee's own pending set (the /groups list's "Your invitations"
        // card + the self-lane's Web gate). Pending rows only, newest first.
        if (string.IsNullOrEmpty(userId))
            return Array.Empty<GroupInvitation>();

        await using var session = store.QuerySession();
        return await session
            .Query<GroupInvitation>()
            .Where(i => i.UserId == userId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.InvitedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupInvitation>> GetPendingInvitationsForGroupAsync(string groupId)
    {
        // The group's pending set (the /groups/{id} invite lane). Pending rows
        // only, oldest first ("who is still holding" order).
        if (string.IsNullOrEmpty(groupId))
            return Array.Empty<GroupInvitation>();

        await using var session = store.QuerySession();
        return await session
            .Query<GroupInvitation>()
            .Where(i => i.GroupId == groupId && i.Status == InvitationStatus.Pending)
            .OrderBy(i => i.InvitedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

}
