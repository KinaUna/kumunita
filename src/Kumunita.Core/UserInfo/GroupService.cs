using Kumunita.Core.Notifications;
using Kumunita.Core.Translations;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// Concrete <see cref="IGroupService"/> (C07, ADR 0286): the group-management
/// cluster split from <see cref="UserInfoService"/>.
/// </summary>

public partial class GroupService(IDocumentStore store, IServiceProvider? services = null) : IGroupService
{
    private readonly UserInfoStore _shared = new(store, services);

    // C06 (ADR 0288) — the one shared translation-variant write lane, one per
    // parent type (Group / Component). The 6 write methods (Add/Update/Remove ×
    // group / community) are thin forwarders into these (the caller's session
    // shape — passed in as a parameter). The name/description surface differs
    // from the posts / events / announcements / pages / projects surfaces: the
    // "content" is (Name, Description), both nullable, gated by "at least a
    // name or a description" (supplied via the descriptor's ValidateContent).
    // The field initializers may reference the primary-constructor `store`
    // (the D6-legal descriptor captures only store + the static standing
    // resolvers, no other instance state).
    private readonly TranslationLane<Group, GroupTranslation> _groupTranslationLane =
        new(new TranslationDescriptor<Group, GroupTranslation>
        {
            TargetKind = "group",
            ActionPrefix = "grouptranslation",
            Noun = "Group",
            ValidateContent = UserInfoStore.NameOrDescriptionRequired,
            LoadStandingExtra = (_, _) => Task.FromResult<object?>(null),
            ResolveStanding = (Group group, object? _, string actorId, IReadOnlySet<string> actorRoles)
                => ResolveGroupTranslationStanding(group.OwnerId, actorId, actorRoles),
            LoadParent = (IDocumentSession session, string id) => session.LoadAsync<Group>(id),
            Find = (IDocumentSession session, string id, string lang) => session.Query<GroupTranslation>()
                .Where(t => t.GroupId == id && t.LanguageCode == lang)
                .FirstOrDefaultAsync(),
            Create = (string id, string lang, string? name, string? description, string actorId, DateTimeOffset created) => new GroupTranslation
            {
                Id = Guid.NewGuid().ToString("N"),
                GroupId = id,
                LanguageCode = lang,
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                AuthorId = actorId,
                Created = created
            },
            ApplyUpdate = (GroupTranslation row, string? name, string? description, string actorId, DateTimeOffset now) =>
            {
                row.Name = string.IsNullOrWhiteSpace(name) ? null : name;
                row.Description = string.IsNullOrWhiteSpace(description) ? null : description;
            }
        });

    /// <inheritdoc />
    public async Task UpdateGroupDescriptionAsync(string groupId, string? description, string updatedBy)
    {
        // Set (or clear with null) Group.Description; load the group in the same
        // session; append AccessAudit (Action "group.update", TargetKind "group",
        // TargetId = groupId) with Via per the derivation rule; one SaveChangesAsync
        // (ADR 0009; the AddGroupMemberAsync lane's shape minus the membership row).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        group.Description = description;
        session.Store(group);

        var via = updatedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? group.OwnerId : updatedBy;

        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = updatedBy,
            EffectivePrincipalId = effective,
            Action = "group.update",
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
    public async Task SetGroupPrivacyAsync(string groupId, bool isPrivate, string updatedBy)
    {
        // ADR 0010: the privacy write lane. Set (or clear) Group.IsPrivate; load
        // the group in the same session; append AccessAudit (Action "group.update"
        // — the same non-lane-specific verb ADR 0009 introduced, one group field
        // changed under an owner ∪ GlobalAdmin standing — TargetKind "group",
        // TargetId = groupId) with Via per the derivation rule; one SaveChangesAsync.
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        group.IsPrivate = isPrivate;
        session.Store(group);

        var via = updatedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? group.OwnerId : updatedBy;

        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = updatedBy,
            EffectivePrincipalId = effective,
            Action = "group.update",
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
    public async Task DeleteGroupAsync(string groupId, string deletedBy)
    {
        // ADR 0093: the owner ∪ GlobalAdmin delete lane. Hard delete — the
        // Group document, its GroupMembership rows, and its GroupInvitation
        // rows go in one session + one SaveChangesAsync (invariant C3); one
        // AccessAudit row (Action "group.delete", TargetKind "group",
        // TargetId = groupId) rides the same transaction with the Owner/Admin
        // Via derivation every other group write lane uses. No cascade into
        // group-scoped Post / Event / GroupTranslation rows (ADR 0093): the
        // group lane's authorization is membership-only (ADR 0013) and the
        // membership rows are gone, so those rows become unreachable and are
        // left in place rather than deleted.
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        // The group document + its membership + its invitation rows.
        session.Delete<Group>(groupId);

        var memberships = await session.Query<GroupMembership>()
            .Where(m => m.GroupId == groupId)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var m in memberships)
            session.Delete<GroupMembership>(m.Id);

        var invitations = await session.Query<GroupInvitation>()
            .Where(i => i.GroupId == groupId)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var i in invitations)
            session.Delete<GroupInvitation>(i.Id);

        // ADR 0094: the group's join-request rows go with it (the same
        // one-row-per-(group, user) cascade as the invitation rows above).
        var joinRequests = await session.Query<GroupJoinRequest>()
            .Where(j => j.GroupId == groupId)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var j in joinRequests)
            session.Delete<GroupJoinRequest>(j.Id);

        var via = deletedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? group.OwnerId : deletedBy;

        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = deletedBy,
            EffectivePrincipalId = effective,
            Action = "group.delete",
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
    public async Task<IReadOnlyList<Group>> GetPublicGroupsAsync()
    {
        // ADR 0010: the audience / grant picker's option source (public groups
        // only) — same shape and ordering as GetAllGroupsAsync (Created desc),
        // but private groups are a back-office organizing unit and stay out of
        // the grant/access lists. No audit row (C-M2·2: a read, not a decision).
        // Live rows (invariant C4): a public↔private flip is live on the next call.
        await using var session = store.QuerySession();
        var groups = await session
            .Query<Group>()
            .Where(g => !g.IsPrivate)
            .ToListAsync()
            .ConfigureAwait(false);

        var byId = groups.ToDictionary(g => g.Id);
        return byId.Values
            .OrderByDescending(g => g.Created)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Group?> GetGroupAsync(string groupId)
    {
        // One-document read (the GetProfileAsync shape on the Group axis).
        // Candidate read, not a decision — no AccessAudit row (C-M2·2 carried).
        await using var session = store.QuerySession();
        return await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupTranslation>> GetGroupTranslationsAsync(string groupId)
    {
        if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("A group id is required.", nameof(groupId));

        await using var session = store.QuerySession();
        return await session
            .Query<GroupTranslation>()
            .Where(t => t.GroupId == groupId)
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GroupTranslation> AddGroupTranslationAsync(
        string groupId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return await _groupTranslationLane.AddAsync(session, groupId, languageCode, name, description, actorId, actorRoles).ConfigureAwait(false);
    }

    /// <summary>
    /// **Updates** the existing <see cref="GroupTranslation"/> row for
    /// (<paramref name="groupId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing is the same as the ADR
    /// 0026 add lane (group owner → Owner; GlobalAdmin / Translator → Admin).
    /// A missing group or a missing row is a <see cref="KeyNotFoundException"/>;
    /// a denied actor throws <see cref="UnauthorizedAccessException"/>. The new
    /// name / description must have at least one non-blank field. One
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task<GroupTranslation> UpdateGroupTranslationAsync(
        string groupId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return await _groupTranslationLane.UpdateAsync(session, groupId, languageCode, name, description, actorId, actorRoles).ConfigureAwait(false);
    }

    /// <summary>
    /// **Removes** the existing <see cref="GroupTranslation"/> row for
    /// (<paramref name="groupId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing is the same as the ADR
    /// 0026 add lane. A hard <c>session.Delete</c>; the trail is preserved by
    /// the <see cref="Authorization.AccessAudit"/> row (action
    /// <c>grouptranslation.remove</c>). A missing group or row is a
    /// <see cref="KeyNotFoundException"/>. One <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task RemoveGroupTranslationAsync(
        string groupId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        await _groupTranslationLane.RemoveAsync(session, groupId, languageCode, actorId, actorRoles).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool CanTranslateGroup(string ownerId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveGroupTranslationStanding(ownerId, actorId, actorRoles) is not null;

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

        // Business key (GroupId, UserId) — the unique index in CoreDocTypes
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

        // ADR 0083 + the GU community-approval lane — the recipient depends
        // on whether <c>userId</c> is a supervised child AND the inviter
        // is NOT one of the child's active guardians:
        //
        // · Supervised child, non-guardian inviter — one row per ACTIVE
        //   guardian (kind <c>guardian.group_invite</c>; the LinkPath /
        //   AcceptPath / DeclinePath all point at
        //   <c>/me/children/{childId}</c>, the manage-child page where the
        //   guardian sees the pending list + the approve / reject buttons —
        //   the GU community-approval lane's shape, the
        //   <see cref="ApproveGroupInvitationAsync"/> G·2 resolver; the
        //   child's own self-lane
        //   <see cref="AcceptGroupInvitationAsync"/> is refused by the GU
        //   gate, so the child is not a valid recipient). The body is the
        //   group name + the child's display name (a curation fact the
        //   parent already sees in their /me/children list — the
        //   <see cref="ChildAccountItem"/> precedent, not a G·1-hiding
        //   content read).
        // · Supervised child, GUARDIAN inviter (a guardian inviting their
        //   own child — the <see cref="AddGroupMemberAsync"/> precedent,
        //   <c>Via: Guardian</c>): a self-notification fan-out would be
        //   noise (the inviter already holds the standing to resolve it),
        //   so fall through to the child-facing lane (kind
        //   <c>group.invite</c>, recipient = the child, the self-lane GET
        //   AcceptPath / DeclinePath). The child's own
        //   <see cref="AcceptGroupInvitationAsync"/> self-lane is still
        //   GU-gated (the guardian must resolve via
        //   <see cref="ApproveGroupInvitationAsync"/> /
        //   <see cref="RejectGroupInvitationAsync"/>) — the
        //   self-notification is only the nudge, not the resolve path.
        // · Non-supervised — the ADR 0083 / ADR 0095 shape unchanged.
        //
        // Either way the emission sits in this session's transaction
        // (invariant C3), the <c>NotificationService</c> is resolved
        // lazily from <c>IServiceProvider</c> (the circular-dependency
        // rationale — the class doc-comment), and pre-ADR 0083 direct-
        // construction test harnesses (services = null) silently skip.
        var activeGuardians = await _shared.ActiveGuardianIdsAsync(session, userId).ConfigureAwait(false);
        if (activeGuardians.Count > 0 && !activeGuardians.Contains(invitedBy))
        {
            var childName = await _shared.ChildDisplayNameAsync(userId).ConfigureAwait(false);
            foreach (var guardianId in activeGuardians)
            {
                await _shared.EmitGuardianNotificationAsync(
                    session, userId, guardianId,
                    Notifications.NotificationKinds.GuardianGroupInvite,
                    groupId,
                    $"{group.Name} — {childName}").ConfigureAwait(false);
            }
        }
        else
        {
            var ns = services?.GetService<INotificationEmitter>();
            if (ns is not null)
            {
                await ns.EmitAsync(
                    session,
                    userId,
                    Notifications.NotificationKinds.GroupInvite,
                    $"notification:{Notifications.NotificationKinds.GroupInvite}:{groupId}:{userId}",
                    group.Name,
                    acceptPath: $"/groups/{groupId}/invitations/accept",
                    declinePath: $"/groups/{groupId}/invitations/decline",
                    ct: CancellationToken.None).ConfigureAwait(false);
            }
        }

        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }


    /// <inheritdoc />
    public async Task<GroupJoinRequest> RequestToJoinGroupAsync(string groupId, string actorId)
    {
        // Self-lane (the requester starts it). Upsert the (group, user) row on
        // the business key; a resolved (Approved / Declined) row resets to the
        // fresh Pending shape — the two resolve stamps are cleared with it.
        // Appends AccessAudit (action "group.join.request", the requester's own
        // standing) in the same session; one SaveChangesAsync (invariant C3).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        var row = await session.Query<GroupJoinRequest>()
            .Where(j => j.GroupId == groupId && j.UserId == actorId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new GroupJoinRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                GroupId = groupId,
                UserId = actorId,
                Status = JoinRequestStatus.Pending,
                RequestedAt = now
            };
        }
        else
        {
            row.Status = JoinRequestStatus.Pending;
            row.RequestedAt = now;
            row.ResolvedAt = null;
            row.ResolvedBy = null;
        }

        session.Store(row);

        // Self-lane audit: the requester's own standing — Via Owner with the
        // requester as all three identities (the accept self-lane's shape).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "group.join.request",
            TargetKind = "group",
            TargetId = groupId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <inheritdoc />
    public async Task ApproveJoinRequestAsync(string groupId, string userId, string resolvedBy)
    {
        // Owner ∪ GlobalAdmin lane (JR·1): resolve the requester's Pending row
        // and land the membership. In the same session (C3): the row moves
        // Pending → Approved, the GroupMembership row is upserted (C4), and the
        // audit row (action "group.join.approve", owner ⇒ Owner else Admin)
        // rides the transaction. One SaveChangesAsync.
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // GU gate (ADR 0028 §C / G·2): a supervised child — one with an active
        // GuardianLink — does not have a self-standing to approve their own
        // request (their membership-landing lane is guardian-mediated, exactly
        // as AcceptGroupInvitationAsync's). Read-only; refuse here.
        var supervised = await session.Query<GuardianLink>()
            .Where(l => l.ChildId == userId && l.Status == GuardianLinkStatus.Active)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (supervised is not null)
            throw new InvalidOperationException(
                $"Account {userId} is supervised; a group join must be approved by their guardian.");

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        var row = await session.Query<GroupJoinRequest>()
            .Where(j => j.GroupId == groupId && j.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException(
                $"No join request for {userId} in group {groupId}");

        if (row.Status != JoinRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Join request {row.Id} is already {row.Status}; only a Pending request can be approved.");

        row.Status = JoinRequestStatus.Approved;
        row.ResolvedAt = now;
        row.ResolvedBy = resolvedBy;
        session.Store(row);

        // The membership lands here — the exact AddGroupMemberAsync upsert shape
        // (strong consistency, invariant C4).
        var membership = await session.Query<GroupMembership>()
            .Where(m => m.GroupId == groupId && m.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (membership is null)
        {
            membership = new GroupMembership
            {
                Id = Guid.NewGuid().ToString("N"),
                GroupId = groupId,
                UserId = userId,
                AddedBy = resolvedBy,
                At = now
            };
        }
        else
        {
            membership.AddedBy = resolvedBy;
            membership.At = now;
        }

        session.Store(membership);

        var via = resolvedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? group.OwnerId : resolvedBy;

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = resolvedBy,
            EffectivePrincipalId = effective,
            Action = "group.join.approve",
            TargetKind = "group",
            TargetId = groupId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    /// <inheritdoc />
    public async Task DeclineJoinRequestAsync(string groupId, string userId, string resolvedBy)
    {
        // Owner ∪ GlobalAdmin lane (JR·1): the same gate + state check as
        // ApproveJoinRequestAsync, but no GroupMembership row is touched — the
        // requester simply never becomes a member. One SaveChangesAsync (C3).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new InvalidOperationException($"Group not found: {groupId}");

        var row = await session.Query<GroupJoinRequest>()
            .Where(j => j.GroupId == groupId && j.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException(
                $"No join request for {userId} in group {groupId}");

        if (row.Status != JoinRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Join request {row.Id} is already {row.Status}; only a Pending request can be declined.");

        row.Status = JoinRequestStatus.Declined;
        row.ResolvedAt = now;
        row.ResolvedBy = resolvedBy;
        session.Store(row);

        var via = resolvedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin;
        var effective = via == Authorization.AccessVia.Owner ? group.OwnerId : resolvedBy;

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = resolvedBy,
            EffectivePrincipalId = effective,
            Action = "group.join.decline",
            TargetKind = "group",
            TargetId = groupId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    /// <inheritdoc />
    public async Task WithdrawJoinRequestAsync(string groupId, string actorId)
    {
        // Self-lane (the requester pulls their own Pending row back). The row
        // must be the actor's own and Pending; it moves to the terminal
        // Withdrawn state (stamped with the requester) — the direct analogue of
        // the m2b owner's CancelGroupInvitationAsync: the row drops off both
        // the requester's "Your join requests" card and the owner's review
        // list, and stays re-requestable (a re-request resets it to Pending).
        // No GroupMembership row is touched. One SaveChangesAsync (C3).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var row = await session.Query<GroupJoinRequest>()
            .Where(j => j.GroupId == groupId && j.UserId == actorId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        // Self-lane (JR·4): a row that isn't the actor's own is an invalid
        // self-lane call (the Web's 404 gate is the first wall; this is the
        // Core's).
        if (row is null || row.UserId != actorId)
            throw new InvalidOperationException(
                $"No join request for {actorId} in group {groupId} to withdraw.");

        if (row.Status != JoinRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Join request {row.Id} is already {row.Status}; only a Pending request can be withdrawn.");

        row.Status = JoinRequestStatus.Withdrawn;
        row.ResolvedAt = now;
        row.ResolvedBy = actorId;
        session.Store(row);

        // Self-lane audit: the requester's own standing (Via Owner, all three
        // identities the requester).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "group.join.withdraw",
            TargetKind = "group",
            TargetId = groupId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupJoinRequest>> GetPendingJoinRequestsForUserAsync(string userId)
    {
        // The requester's own pending set (the /groups list's "Your join
        // requests" card + the withdraw self-lane's Web gate). Pending rows
        // only, newest first.
        if (string.IsNullOrEmpty(userId))
            return Array.Empty<GroupJoinRequest>();

        await using var session = store.QuerySession();
        return await session
            .Query<GroupJoinRequest>()
            .Where(j => j.UserId == userId && j.Status == JoinRequestStatus.Pending)
            .OrderByDescending(j => j.RequestedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupJoinRequest>> GetPendingJoinRequestsForGroupAsync(string groupId)
    {
        // The group's pending set (the /groups/{id} review surface). Pending
        // rows only, oldest first ("who is still holding" order).
        if (string.IsNullOrEmpty(groupId))
            return Array.Empty<GroupJoinRequest>();

        await using var session = store.QuerySession();
        return await session
            .Query<GroupJoinRequest>()
            .Where(j => j.GroupId == groupId && j.Status == JoinRequestStatus.Pending)
            .OrderBy(j => j.RequestedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The ADR 0026 group-translation standing resolver (shared by
    /// <see cref="AddGroupTranslationAsync"/> and <see
    /// cref="CanTranslateGroup"/>). Returns the <see cref="Authorization
    /// .AccessVia"/> the actor qualifies under, or <c>null</c> to deny.
    /// Precedence (most specific standing first, so the audit row records the
    /// narrowest right that applied): the group's <b>owner</b>
    /// (<see cref="Authorization.AccessVia.Owner"/>); a <b>GlobalAdmin</b> or a
    /// <b>Translator</b> (both <see cref="Authorization.AccessVia.Admin"/>).
    /// A group **member** is not a standing (membership is not the right to
    /// rename the group for others); a component-moderator claim does not
    /// qualify (the group lane has no component-moderator standing, ADR 0007).
    /// </summary>
    private static Authorization.AccessVia? ResolveGroupTranslationStanding(
        string ownerId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.Equals(ownerId, actorId, StringComparison.Ordinal))
            return Authorization.AccessVia.Owner;
        if (actorRoles.Contains(Identity.Roles.GlobalAdmin))
            return Authorization.AccessVia.Admin;
        if (actorRoles.Contains(Identity.Roles.Translator))
            return Authorization.AccessVia.Admin;
        return null;
    }

}
