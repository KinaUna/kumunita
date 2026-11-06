using Kumunita.Core.Notifications;
using Kumunita.Core.Translations;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// The <c>/guardian</c> group + community approval lane — the
/// approve/reject group-invitation + approve/decline
/// community-membership-request actions (ADR 0028
/// guardian-controls, ADR 0012 community-membership).
/// A partial class of <see cref="GuardianService"/> extracted
/// so the main file stays under the 600 <c>cs_file</c> ceiling
/// (C3-04).
/// </summary>
public partial class GuardianService
{
    /// <inheritdoc />
    public async Task<GroupInvitation> ApproveGroupInvitationAsync(string groupId, string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("Group id is required.", nameof(groupId));
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate (G·2 live / G·3 deny-by-default): an ACTIVE link for
        // this exact (guardian, child) pair — the G3_NonChildTargetIsRefused
        // precondition. No link ⇒ refused (the Web's 404).
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        // Precondition: a PENDING invitation on the CHILD (keyed on childId —
        // distinct from the no-standing gate above).
        var row = await session.Query<GroupInvitation>()
            .Where(i => i.GroupId == groupId && i.UserId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException(
                $"No group invitation for {childId} in group {groupId}");

        if (row.Status != InvitationStatus.Pending)
            throw new InvalidOperationException(
                $"Invitation {row.Id} is already {row.Status}; only a Pending invitation can be approved.");

        // The accept write path, reused verbatim — the membership lands exactly
        // as AcceptGroupInvitationAsync writes it; the only differences are the
        // child-keyed row, ResolvedBy = the guardian, and the audit verb/Via.
        row.Status = InvitationStatus.Accepted;
        row.ResolvedAt = now;
        row.ResolvedBy = guardianId;
        session.Store(row);

        var membership = await session.Query<GroupMembership>()
            .Where(m => m.GroupId == groupId && m.UserId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (membership is null)
        {
            membership = new GroupMembership
            {
                Id = Guid.NewGuid().ToString("N"),
                GroupId = groupId,
                UserId = childId,
                AddedBy = guardianId,
                At = now
            };
        }
        else
        {
            membership.AddedBy = guardianId;
            membership.At = now;
        }

        session.Store(membership);

        // Guardian approval audit: the guardian's standing (all three
        // identities the guardian; the target is the group).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "group.invite.approve",
            TargetKind = "group",
            TargetId = groupId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <inheritdoc />
    public async Task RejectGroupInvitationAsync(string groupId, string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("Group id is required.", nameof(groupId));
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate (G·2 live / G·3 deny-by-default): an ACTIVE link for
        // this exact (guardian, child) pair.
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        // Precondition: a PENDING invitation on the CHILD (the
        // ApproveGroupInvitationAsync G·2 shape; the decline sibling).
        var row = await session.Query<GroupInvitation>()
            .Where(i => i.GroupId == groupId && i.UserId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException(
                $"No group invitation for {childId} in group {groupId}");

        if (row.Status != InvitationStatus.Pending)
            throw new InvalidOperationException(
                $"Invitation {row.Id} is already {row.Status}; only a Pending invitation can be rejected.");

        // The decline write path (the ApproveGroupInvitationAsync shape
        // minus the membership write): the row moves Pending → Declined,
        // ResolvedBy = the guardian. No GroupMembership row is touched.
        row.Status = InvitationStatus.Declined;
        row.ResolvedAt = now;
        row.ResolvedBy = guardianId;
        session.Store(row);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "group.invite.reject",
            TargetKind = "group",
            TargetId = groupId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    /// <inheritdoc />
    public async Task ApproveCommunityMembershipRequestAsync(
        string componentId, string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate (G·2 live / G·3 deny-by-default): an ACTIVE link for
        // this exact (guardian, child) pair.
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        var row = await session.Query<CommunityMembershipRequest>()
            .Where(r => r.ComponentId == componentId && r.UserId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException(
                $"No community membership request for {childId} in community {componentId}");

        if (row.Status != CommunityMembershipRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Community membership request {row.Id} is already {row.Status}; only a Pending request can be approved.");

        // The approve write path (the sibling of ApproveGroupInvitationAsync):
        // the membership row lands exactly as SetCommunityMembershipAsync
        // writes it (the idempotent business-key upsert); the differences are
        // the child-keyed row, ResolvedBy = the guardian, and the audit verb.
        row.Status = CommunityMembershipRequestStatus.Approved;
        row.ResolvedAt = now;
        row.ResolvedBy = guardianId;
        session.Store(row);

        var membership = await session.Query<ComponentMembership>()
            .Where(m => m.ComponentId == componentId && m.UserId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (membership is null)
        {
            membership = new ComponentMembership
            {
                Id = Guid.NewGuid().ToString("N"),
                ComponentId = componentId,
                UserId = childId,
                AddedBy = guardianId,
                At = now
            };
        }
        else
        {
            membership.AddedBy = guardianId;
            membership.At = now;
        }

        session.Store(membership);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "community.membership.approve",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    /// <inheritdoc />
    public async Task DeclineCommunityMembershipRequestAsync(
        string componentId, string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        var row = await session.Query<CommunityMembershipRequest>()
            .Where(r => r.ComponentId == componentId && r.UserId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException(
                $"No community membership request for {childId} in community {componentId}");

        if (row.Status != CommunityMembershipRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Community membership request {row.Id} is already {row.Status}; only a Pending request can be declined.");

        row.Status = CommunityMembershipRequestStatus.Declined;
        row.ResolvedAt = now;
        row.ResolvedBy = guardianId;
        session.Store(row);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "community.membership.decline",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }
}
