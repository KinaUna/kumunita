using Marten;
using Marten.Services;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// Concrete <see cref="IUserInfoService"/> (M1 step 4): the UserInfoModule's
/// strong-consistency storage behind the frozen ADR 0006 §A surface.
/// <para>
/// Session shape (invariant C3 — same transaction): every mutating call runs in a
/// single document session and ends in one <c>SaveChangesAsync</c>, so the domain
/// write and the accompanying <see cref="Authorization.AccessAudit"/> row (the
/// admin-action lane) commit atomically — a failed save rolls back both.
/// </para>
/// <para>
/// Reads touch the live rows directly (no projection, no cache — invariant C4): a
/// change is live on the very next call.
/// </para>
/// <para>
/// The admin-action audit lane appends a <see cref="Authorization.AccessAudit"/> row:
/// <c>group.add-member</c> / <c>group.remove-member</c> (<c>TargetKind</c> "group"),
/// <c>delegation.grant</c> / <c>delegation.revoke</c> (targetKind "delegation_grant"),
/// <c>moderator-access</c> (targetKind "component", <see cref="Authorization.AccessVia.Admin"/>).
/// <see cref="UpsertProfileAsync"/> and <see cref="SeedComponentsAsync"/> are not
/// access decisions and append no audit row.
/// </para>
/// <para>
/// <see cref="Authorization.AccessVia"/> derivation (service-level interpretation; if
/// step 6/8 surfaces a case the rule misderives, an overload with an explicit
/// <c>via</c> is the ADR 0006-E-compatible, non-breaking lane):
/// </para>
/// <list type="bullet">
/// <item>group add/remove — load the group's <see cref="Group.OwnerId"/> in the same
/// session; actor (<c>addedBy</c>/<c>removedBy</c>) equals it →
/// <see cref="Authorization.AccessVia.Owner"/>, else <see cref="Authorization.AccessVia.Admin"/>.</item>
/// <item>delegation grant/revoke — the recorded actor is <c>ownerId</c> (grant) /
/// <c>revokedBy</c> (revoke); equals the grant's <see cref="DelegationGrant.OwnerId"/> →
/// <see cref="Authorization.AccessVia.Owner"/>, else <see cref="Authorization.AccessVia.Admin"/>
/// (requires one extra session load — acceptable for a rare admin action).</item>
/// </list>
/// </summary>
public sealed class UserInfoService(IDocumentStore store) : IUserInfoService
{
    // ── Read paths (plan step 3 — live-row reads, invariant C4) ───────────

    /// <inheritdoc />
    public async Task<Profile?> GetProfileAsync(string subjectId)
    {
        // Single document read — Profile's identity is SubjectId (pinned in
        // M1DocTypes.Configure), so the equality probe lands on the document id.
        await using var session = store.QuerySession();
        return await session
            .Query<Profile>()
            .Where(p => p.SubjectId == subjectId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Profile>> GetProfilesAsync(bool verifiedOnly)
    {
        // Live rows (invariant C4); candidate set, NOT a visible set (§4.3 / C-M2·2)
        // — no audit row here (invariant C3), visibility is decided by the caller
        // via IAuthorizationService (C6 shared matching pass).
        await using var session = store.QuerySession();
        var profiles = verifiedOnly
            ? await session.Query<Profile>().Where(p => p.Verified).ToListAsync().ConfigureAwait(false)
            : await session.Query<Profile>().ToListAsync().ConfigureAwait(false);
        return profiles;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Component>> GetComponentsAsync(bool enabledOnly)
    {
        // M3 ADD (ADR 0006-E lane, mirrors M2's GetProfilesAsync U3): the composer's
        // component picker / /community/{id} grouping / the feed's candidate
        // filter (M3 design §2.3). A candidate set, NOT a visible set — no
        // AccessAudit row (C-M3·2); visibility is the caller's job via
        // IAuthorizationService (C3). Live rows, no projection, no cache (C4).
        await using var session = store.QuerySession();
        var components = enabledOnly
            ? await session.Query<Component>().Where(c => c.Enabled).ToListAsync().ConfigureAwait(false)
            : await session.Query<Component>().ToListAsync().ConfigureAwait(false);
        return components;
    }

    /// <inheritdoc />
    public async Task<HashSet<string>> GetGroupIdsAsync(string userId)
    {
        // Live membership rows, no projection (invariant C4): a change is live on
        // the very next call.
        await using var session = store.QuerySession();
        var ids = await session
            .Query<GroupMembership>()
            .Where(m => m.UserId == userId)
            .ToListAsync()
            .ConfigureAwait(false);
        return new HashSet<string>(ids.Select(m => m.GroupId));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Group>> GetGroupsForUserAsync(string userId)
    {
        // F14 (M2 design doc §2.2): owner ∪ member, deduped, sorted by Created desc.
        // Live rows (invariant C4) — a membership add/remove in the same commit is
        // live on the *very next* call. No audit row (M2 C-M2·2: a read, not a
        // decision). Single QuerySession (C3: read-only reads stay in the live-row
        // lane — no transaction needed).
        if (string.IsNullOrEmpty(userId))
            return Array.Empty<Group>();

        await using var session = store.QuerySession();

        // Two live-row reads, one session: (a) groups whose OwnerId is the user,
        // (b) membership rows for this user, from which we resolve group ids.
        var owned = await session
            .Query<Group>()
            .Where(g => g.OwnerId == userId)
            .ToListAsync()
            .ConfigureAwait(false);

        var memberships = await session
            .Query<GroupMembership>()
            .Where(m => m.UserId == userId)
            .ToListAsync()
            .ConfigureAwait(false);

        // Union of group ids (dedupe by id), preserving a stable id → Group lookup.
        var byId = owned
            .ToDictionary(g => g.Id);

        var missingIds = memberships
            .Select(m => m.GroupId)
            .Where(id => !byId.ContainsKey(id))
            .Distinct()
            .ToList();

        if (missingIds.Count > 0)
        {
            var groups = await session
                .Query<Group>()
                .Where(g => missingIds.Contains(g.Id))
                .ToListAsync()
                .ConfigureAwait(false);
            foreach (var g in groups)
                byId[g.Id] = g;
        }

        // Sort by Created desc ("most recently created first" — the plan-U9 pin).
        return byId.Values
            .OrderByDescending(g => g.Created)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Group>> GetAllGroupsAsync()
    {
        // The profile grant picker's option source (the M2 editor's UX
        // surface — every Group document, no membership filter). Live rows
        // (invariant C4): a created group is visible on the next read.
        // No audit row (C-M2·2: a read, not a decision). Stable id → Group
        // lookup, sorted by Created desc (the plan-U9 "most recently
        // created first" ordering the Web surface expects).
        await using var session = store.QuerySession();
        var groups = await session
            .Query<Group>()
            .ToListAsync()
            .ConfigureAwait(false);

        var byId = groups.ToDictionary(g => g.Id);
        return byId.Values
            .OrderByDescending(g => g.Created)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupMembership>> GetGroupMembersAsync(string groupId)
    {
        // F14 (M2 design doc §2.2; U9's GroupViewModel.MemberCount + U10's
        // Detail.Members both live off this one read lane — no drift churn).
        // Live rows (invariant C4): an add/remove is live on the next call.
        // No audit row (M2 C-M2·2: a read, not a decision).
        if (string.IsNullOrEmpty(groupId))
            return Array.Empty<GroupMembership>();

        await using var session = store.QuerySession();
        return await session
            .Query<GroupMembership>()
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.At)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DelegationGrant?> GetActiveGrantAsync(string delegateId)
    {
        // Live rows (invariant C4); "active" = DelegationGrant.IsActiveAt — the
        // single source of truth (invariant C2: granted to that account, within
        // [From, To], not revoked). Null when the delegate has no active grant
        // (they act as themselves).
        await using var session = store.QuerySession();
        var grants = await session
            .Query<DelegationGrant>()
            .Where(g => g.DelegateId == delegateId)
            .ToListAsync()
            .ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        return grants.FirstOrDefault(g => g.IsActiveAt(delegateId, now));
    }

    // ── Group lifecycle (plan step 4 — one session + one SaveChangesAsync) ──

    /// <inheritdoc />
    public async Task<Group> CreateGroupAsync(string ownerId, string name, string? description)
    {
        // New Group (guid) + GroupMembership(owner → owner) in one session; no audit row.
        var now = DateTimeOffset.UtcNow;
        var group = new Group
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Description = description,
            OwnerId = ownerId,
            Created = now
        };

        var membership = new GroupMembership
        {
            Id = Guid.NewGuid().ToString("N"),
            GroupId = group.Id,
            UserId = ownerId,
            AddedBy = ownerId,
            At = now
        };

        // Write in a single Marten session and commit.
        await using var session = store.OpenSession(new SessionOptions());
        session.Store(group);
        session.Store(membership);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return group;
    }

    /// <inheritdoc />
    public async Task AddGroupMemberAsync(string groupId, string userId, string addedBy)
    {
        // Upsert GroupMembership by (group, user); load the group in the same session;
        // append AccessAudit (Action "group.add-member", TargetKind "group",
        // TargetId = groupId) with Via per the derivation rule; one SaveChangesAsync.
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        // find existing membership by business key
        var existing = await session.Query<GroupMembership>()
            .Where(m => m.GroupId == groupId && m.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (existing is null)
        {
            existing = new GroupMembership
            {
                Id = Guid.NewGuid().ToString("N"),
                GroupId = groupId,
                UserId = userId,
                AddedBy = addedBy,
                At = now
            };
        }
        else
        {
            // Update metadata for idempotence
            existing.AddedBy = addedBy;
            existing.At = now;
        }

        session.Store(existing);

        var via = addedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? group.OwnerId : addedBy;

        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = addedBy,
            EffectivePrincipalId = effective,
            Action = "group.add-member",
            TargetKind = "group",
            TargetId = groupId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    /// <inheritdoc />
    public async Task RemoveGroupMemberAsync(string groupId, string userId, string removedBy)
    {
        // Delete GroupMembership by (group, user) — strong consistency (invariant C4);
        // append AccessAudit (Action "group.remove-member", ActorId = removedBy, same
        // Via rule as add); one SaveChangesAsync.
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        var membership = await session.Query<GroupMembership>()
            .Where(m => m.GroupId == groupId && m.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (membership is not null)
        {
            session.Delete<GroupMembership>(membership.Id);
        }

        var via = removedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? group.OwnerId : removedBy;

        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = removedBy,
            EffectivePrincipalId = effective,
            Action = "group.remove-member",
            TargetKind = "group",
            TargetId = groupId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    // ── M2b: group invitations (docs/design/m2b-group-invitations.md;
    // one session + one SaveChangesAsync per call, mirroring the M1 group
    // lifecycle shape above — invariants C-M2b·1..3) ──────────────────

    /// <inheritdoc />
    public async Task<Group?> GetGroupAsync(string groupId)
    {
        // One-document read (the GetProfileAsync shape on the Group axis).
        // Candidate read, not a decision — no AccessAudit row (C-M2·2 carried).
        await using var session = store.QuerySession();
        return await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GroupInvitation> InviteGroupMemberAsync(string groupId, string userId, string invitedBy)
    {
        // Upsert the (group, user) invitation row; load the group's OwnerId in
        // the same session for the Via derivation (C-M2b·1: the audit lane is
        // derived exactly like AddGroupMemberAsync — invitedBy == OwnerId ⇒
        // Owner, else Admin); append AccessAudit (action "group.invite");
        // one SaveChangesAsync (invariant C3).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        // Business key (GroupId, UserId) — the unique index in M1DocTypes
        // enforces "one row per (group, user)" (C-M2b·3, the same pair
        // convention as GroupMembership).
        var row = await session.Query<GroupInvitation>()
            .Where(i => i.GroupId == groupId && i.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new GroupInvitation
            {
                Id = Guid.NewGuid().ToString("N"),
                GroupId = groupId,
                UserId = userId,
                InvitedBy = invitedBy,
                Status = InvitationStatus.Pending,
                InvitedAt = now
            };
        }
        else
        {
            // Re-invite (C-M2b·3): a resolved row (or an already-Pending
            // re-stamp) resets to the fresh Pending shape — the two resolve
            // stamps are cleared with it.
            row.InvitedBy = invitedBy;
            row.Status = InvitationStatus.Pending;
            row.InvitedAt = now;
            row.ResolvedAt = null;
            row.ResolvedBy = null;
        }

        session.Store(row);

        var via = invitedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? group.OwnerId : invitedBy;

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = invitedBy,
            EffectivePrincipalId = effective,
            Action = "group.invite",
            TargetKind = "group",
            TargetId = groupId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

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

    // ── M2b read lanes (candidate reads — no AccessAudit row, C-M2·2
    // carried; live rows, invariant C4) ─────────────────────────────────

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

    // ── Delegation (plan step 5 — one session per call) ──────────────────

    /// <inheritdoc />
    public async Task<DelegationGrant> GrantDelegationAsync(string ownerId, string delegateId,
        IReadOnlyList<string> scope, DateTimeOffset from, DateTimeOffset? to)
    {
        // New DelegationGrant (window [from, to] + scope, To null = open-ended);
        // grantor's identity is ownerId (the interface signature's only identity
        // param) — the derivation rule records them as the actor; they are also the
        // effective standing the delegation borrows, so Via and EffectivePrincipalId
        // both point at ownerId.
        var now = DateTimeOffset.UtcNow;
        var grant = new DelegationGrant
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = ownerId,
            DelegateId = delegateId,
            Scope = [.. scope],
            From = from,
            To = to
        };

        await using var session = store.OpenSession(new SessionOptions());
        session.Store(grant);
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = ownerId,
            EffectivePrincipalId = ownerId,
            Action = "delegation.grant",
            TargetKind = "delegation_grant",
            TargetId = grant.Id,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync().ConfigureAwait(false);
        return grant;
    }

    /// <inheritdoc />
    public async Task RevokeDelegationAsync(string grantId, string revokedBy)
    {
        // Load grant by id; set RevokedBy = revokedBy (row is kept — history);
        // append AccessAudit (Action "delegation.revoke", ActorId = revokedBy) with
        // Via per the derivation rule; one SaveChangesAsync.
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var grant = await session.LoadAsync<DelegationGrant>(grantId).ConfigureAwait(false);
        if (grant is null)
            throw new InvalidOperationException($"DelegationGrant not found: {grantId}");

        grant.RevokedBy = revokedBy;
        session.Store(grant);

        var via = revokedBy == grant.OwnerId
            ? Authorization.AccessVia.Owner
            : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? grant.OwnerId : revokedBy;

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = revokedBy,
            EffectivePrincipalId = effective,
            Action = "delegation.revoke",
            TargetKind = "delegation_grant",
            TargetId = grantId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    // ── Components + profile (plan step 6) ───────────────────────────────

    /// <inheritdoc />
    public async Task UpsertProfileAsync(Profile profile, ProfileUpdate patch)
    {
        // Load-or-create by SubjectId (the pinned document identity). When creating, the
        // `profile` argument is the base record (its fields define the initial row); the
        // `patch`'s non-null fields then take priority over every source (the M1 bootstrap
        // surface). One SaveChangesAsync; no audit row (called by the Identity lifecycle /
        // bootstrap surface — not an access decision, so invariant C3's audit lane does not
        // apply here).
        var subjectId = profile.SubjectId;

        await using var session = store.OpenSession(new SessionOptions());
        var existing = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);

        var doc = existing ?? new Profile
        {
            SubjectId = subjectId,
            ExternalId = profile.ExternalId,
            HouseholdId = profile.HouseholdId,
            DisplayName = profile.DisplayName,
            Verified = profile.Verified,
            Visibility = profile.Visibility,
            ContactVisibility = profile.ContactVisibility,
            Email = profile.Email,
            Phone = profile.Phone,
            Address = profile.Address
        };

        // Patch wins on every non-null field; a null field leaves the current value untouched.
        if (patch.DisplayName is not null) doc.DisplayName = patch.DisplayName;
        if (patch.Email is not null) doc.Email = patch.Email;
        if (patch.Phone is not null) doc.Phone = patch.Phone;
        if (patch.Visibility is not null) doc.Visibility = patch.Visibility;
        if (patch.ContactVisibility is not null) doc.ContactVisibility = patch.ContactVisibility;
        if (patch.Address is not null) doc.Address = patch.Address;

        session.Store(doc);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Component>> SeedComponentsAsync()
    {
        // Upsert the four defaults by their stable identity — for the known set,
        // Component.Id is the key ("safety"/"maintenance"/"social"/"governance"), so
        // "upsert by key" is a plain identity-keyed read-then-decide. Create absent
        // rows (their `ModeratorAccess` default of `false` pins invariant C5); leave
        // existing rows untouched — only SetComponentModeratorAccessAsync flips that
        // flag — so an idempotent re-run never resets a deliberate ON. One
        // SaveChangesAsync; no audit row (bootstrap, not an access decision).
        var seeds = new[]
        {
            new Component { Id = "safety", Name = "Safety" },
            new Component { Id = "maintenance", Name = "Maintenance" },
            new Component { Id = "social", Name = "Social" },
            new Component { Id = "governance", Name = "Governance" },
        };

        var result = new List<Component>(seeds.Length);

        await using var session = store.OpenSession(new SessionOptions());
        foreach (var seed in seeds)
        {
            var existing = await session.LoadAsync<Component>(seed.Id).ConfigureAwait(false);
            if (existing is not null)
            {
                // Present already: return it unchanged; only the flag path may touch it.
                result.Add(existing);
                continue;
            }
            session.Store(seed);
            result.Add(seed);
        }

        await session.SaveChangesAsync().ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task SetComponentModeratorAccessAsync(string componentId, bool on, string actorId)
    {
        // The standing-moderator-scope path (ADR 0003, invariant C5 — the ONLY writer that
        // may flip Component.ModeratorAccess): load the component, set the flag, append the
        // audit row (Action "moderator-access", TargetKind "component", Via = Admin — the
        // interface doc pins this: only a GlobalAdmin reaches it — ActorId = actorId) in the
        // SAME session, one SaveChangesAsync (invariant C3 — flag flip and audit commit
        // atomically).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Component not found: {componentId}");

        component.ModeratorAccess = on;
        session.Store(component);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "moderator-access",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModeratorAssignment>> GetAssignmentsAsync(string userId)
    {
        // Live-row read (invariant C4): the account's component-scope assignments (ADR 0003's
        // named-scope rows) at this instant — no projection, no cache.
        await using var session = store.QuerySession();
        return await session
            .Query<ModeratorAssignment>()
            .Where(a => a.UserId == userId)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    // ── Admin community management (add / edit / enable-disable, GlobalAdmin
    // surface) — the same admin-action audit lane as the methods above: the
    // <see cref="Authorization.AccessAudit"/> row (via: Admin) and the domain
    // row commit atomically in one session (invariant C3). ──────────────

    /// <inheritdoc />
    public async Task<Component> CreateCommunityAsync(string name, string? description, string actorId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Community name is required.", nameof(name));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        // Slug the name and append a short suffix for collision safety. The
        // slug itself is still stable and human-readable (safety, maintenance,
        // social, governance keep their hand-set ids — the slug form never
        // collides with them because we add the suffix).
        var id = NewCommunityId(name);

        // SortOrder: append after the current maximum so the new community
        // lands at the end of the existing list (a sensible default the admin
        // can re-sort via UpdateCommunityAsync).
        int maxSort;
        await using (var q = store.QuerySession())
        {
            var existing = await q.Query<Component>().ToListAsync().ConfigureAwait(false);
            maxSort = existing.Count == 0 ? -1 : existing.Select(c => c.SortOrder).Max();
        }

        var now = DateTimeOffset.UtcNow;
        var component = new Component
        {
            Id = id,
            Name = name,
            Description = description,
            // Icon stays null — the /admin form does not expose it; a future
            // UI (e.g. a picker) can write it via SetIconAsync-style extension.
            SortOrder = maxSort + 1,
            Enabled = true,
            ModeratorAccess = false // C5: OFF by default; only UpdateCommunityAsync may flip it on later.
        };

        await using var session = store.OpenSession(new SessionOptions());
        session.Store(component);
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "community.add",
            TargetKind = "component",
            TargetId = id,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync().ConfigureAwait(false);
        return component;
    }

    /// <inheritdoc />
    public async Task UpdateCommunityAsync(string componentId,
        string? name, string? description,
        int? sortOrder, bool? moderatorAccess, bool? enabled,
        string actorId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));
        if (name is not null && string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be blank.", nameof(name));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        // Patch semantics: a null argument is "keep as-is", not "clear". This
        // is what lets the /admin "edit" form round-trip safely — an unchecked
        // checkbox doesn't erase the current value.
        if (name is not null) component.Name = name;
        if (description is not null) component.Description = description;
        if (sortOrder is not null) component.SortOrder = sortOrder.Value;
        if (moderatorAccess is not null) component.ModeratorAccess = moderatorAccess.Value;
        if (enabled is not null) component.Enabled = enabled.Value;

        session.Store(component);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "community.update",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetCommunityEnabledAsync(string componentId, bool enabled, string actorId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        component.Enabled = enabled;
        session.Store(component);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = enabled ? "community.enable" : "community.disable",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    // ── Community membership (posting right) — the write / read lanes
    // for the ComponentMembership row. Admin-action lane (via:Admin) on the
    // writes; the read is a candidate read (no audit row — same audit pin as
    // GetGroupIdsAsync), matching the ADR 0006-A/D pattern used for Group
    // membership.

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> GetCommunityIdsAsync(string userId)
    {
        // Strong consistency (invariant C4): live rows, no projection, no cache.
        // No AccessAudit row is appended here — this is a candidate read, mirroring
        // GetGroupIdsAsync's audit pin (the write lanes SetCommunityMembershipAsync /
        // ClearCommunityMembershipAsync are the admin audited actions).
        if (string.IsNullOrWhiteSpace(userId))
            return System.Array.Empty<string>();

        await using var session = store.QuerySession();
        return (await session
            .Query<ComponentMembership>()
            .Where(m => m.UserId == userId)
            .Select(m => m.ComponentId)
            .ToListAsync()
            .ConfigureAwait(false)) as IReadOnlyCollection<string>;
    }

    /// <inheritdoc />
    public async Task SetCommunityMembershipAsync(string componentId, string userId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", nameof(userId));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // The component must exist (a membership on a missing component is a data
        // bug, not a no-op).
        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        // Upsert by business key (component, user); the DB unique index
        // guarantees at most one row per pair, so this is idempotent.
        var existing = await session.Query<ComponentMembership>()
            .Where(m => m.ComponentId == componentId && m.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (existing is null)
        {
            existing = new ComponentMembership
            {
                Id = Guid.NewGuid().ToString("N"),
                ComponentId = componentId,
                UserId = userId,
                AddedBy = actorId,
                At = now
            };
        }
        else
        {
            // Refresh the idempotency metadata on a re-add (no new row).
            existing.AddedBy = actorId;
            existing.At = now;
        }

        session.Store(existing);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "community.add-member",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearCommunityMembershipAsync(string componentId, string userId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", nameof(userId));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Delete (if any) — strong consistency (invariant C4): the next
        // GetCommunityIdsAsync is live; an absent row is a no-op (not an error).
        var membership = await session.Query<ComponentMembership>()
            .Where(m => m.ComponentId == componentId && m.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (membership is not null)
            session.Delete(membership);

        // Always audit the admin action (allow or no-op): the same audit lane
        // as the "add" lane, with a distinct action string so /admin/audit can
        // distinguish add from remove.
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "community.remove-member",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The <c>Component.Id</c> for a new admin-created community: the lowercased
    /// name, non-alphanumeric runs collapsed to a single "<c>-</c>", with a short
    /// 4-hex-digit random suffix appended to guarantee uniqueness against
    /// concurrent admin adds. (The four seeded ids — <c>safety</c>,
    /// <c>maintenance</c>, <c>social</c>, <c>governance</c> — keep their
    /// hand-set identity; this slug form can never collide with them because
    /// it always carries the trailing "-xxxx" suffix.)
    /// </summary>
    private static string NewCommunityId(string name)
    {
        const char dash = '-';
        var sb = new System.Text.StringBuilder(name.Length + 5);
        var lastDash = false;
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append(dash);
                lastDash = true;
            }
        }
        if (sb.Length > 0 && sb[^1] == dash) sb.Length--;
        if (sb.Length == 0) sb.Append("x"); // guard against an all-punctuation name.

        var suffix = Guid.NewGuid().ToString("N")[..4];
        return $"{sb.ToString()}-{suffix}";
    }
}
