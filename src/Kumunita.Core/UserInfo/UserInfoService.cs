using Kumunita.Core.Notifications;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

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
/// <para>
/// ADR 0083 — the group-membership notification emitters: <c>AddGroupMemberAsync</c>
/// (kind <c>group.added</c>) and <c>InviteGroupMemberAsync</c> (kind
/// <c>group.invite</c>) both resolve the recipient's inbox row + conditional
/// email through the same
/// <see cref="Notifications.NotificationService"/> emitter seam the M6
/// PostService / EventService / IdentityService lanes use (F1 / F2 / D3). The
/// seam is **optional** (CS1736) so the 181 direct-construction test harnesses
/// that build <c>UserInfoService(store)</c> positionally keep compiling
/// unchanged; the DI registration resolves the live instance automatically
/// (the IdentityService precedent in <c>DependencyInjection.cs</c>). The
/// emission sits in the caller's transaction (the same session the domain
/// write uses), so the inbox row + outbox row commit atomically with the
/// membership row (invariant C3).
/// </para>
/// <para>
/// **Circular-dependency avoidance:** <c>NotificationService</c> depends on
/// <c>IUserInfoService</c> (for the recipient's <c>Profile.EmailLanguage</c>
/// + <c>Email</c>), so a direct <c>NotificationService?</c> parameter on
/// <c>UserInfoService</c> would create a DI cycle
/// (<c>IUserInfoService → UserInfoService → NotificationService →
/// IUserInfoService</c>). Instead, the seam is
/// <c>IServiceProvider?</c> (always resolvable, no cycle) and the
/// <c>NotificationService</c> instance is resolved lazily at emission time
/// (runtime, not construction) via
/// <c>services.GetService&lt;NotificationService&gt;()</c>. The 181
/// direct-construction test harnesses that build
/// <c>UserInfoService(store)</c> positionally keep compiling unchanged
/// (the <c>IServiceProvider?</c> has a <c>null</c> default — those harnesses
/// simply get no emission, which is correct for pre-ADR 0083 tests).
/// </para>
/// </summary>
public sealed class UserInfoService(IDocumentStore store, IServiceProvider? services = null) : IUserInfoService
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
    public async Task<Group> CreateGroupAsync(string ownerId, string name, string? description, bool isPrivate = false)
    {
        // New Group (guid) + GroupMembership(owner → owner) in one session; no audit row.
        var now = DateTimeOffset.UtcNow;
        var group = new Group
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Description = description,
            IsPrivate = isPrivate,
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

        // GU (ADR 0028): the guardian base is the narrowest — an active link
        // over <paramref name="userId"/> (the child) wins and records
        // <c>Via: Guardian</c>; otherwise the existing Owner / Admin base applies.
        var via = await GateGuardianStandingAsync(addedBy, userId)
            ?? (addedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin);
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

        // ADR 0083 — notify the newly added resident (kind group.added; the
        // recipient is <c>userId</c>, not <c>addedBy</c>). The emission sits
        // in this session's transaction so the inbox row + outbox row commit
        // atomically with the membership row (invariant C3). The
        // <c>NotificationService</c> is resolved lazily from
        // <c>IServiceProvider</c> (not at construction — see the class
        // doc-comment for the circular-dependency rationale). The 181
        // pre-ADR 0083 direct-construction test harnesses that build
        // <c>UserInfoService(store)</c> positionally pass
        // <c>services = null</c>, so the <c>GetService</c> call is a no-op
        // and the emission is silently skipped.
        var ns = services?.GetService<Notifications.NotificationService>();
        if (ns is not null)
        {
            await ns.EmitAsync(
                session,
                userId,
                Notifications.NotificationKinds.GroupAdded,
                $"notification:{Notifications.NotificationKinds.GroupAdded}:{groupId}:{userId}",
                group.Name,
                CancellationToken.None).ConfigureAwait(false);
        }

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

        // GU (ADR 0028): the guardian base is the narrowest — an active link
        // over <paramref name="userId"/> (the child) wins and records
        // <c>Via: Guardian</c>; otherwise the existing Owner / Admin base applies.
        var via = await GateGuardianStandingAsync(removedBy, userId)
            ?? (removedBy == group.OwnerId ? Authorization.AccessVia.Owner : Authorization.AccessVia.Admin);
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

    // ── ADR 0026 — group name/description translations ─────────────────────
    // Mirrors the ADR 0022 post-translation lane (PostService), in this context:
    // a GroupTranslation row (at most one per language, the M1DocTypes unique
    // index) added by the group's owner (Via: Owner) or a GlobalAdmin /
    // Translator (Via: Admin), a hand-written AccessAudit row in the same
    // session (C3), and a plain read seam (no decision, no audit — inherits the
    // group's owner∪member reach). Add-only (no edit/delete seam).

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
        if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("A group id is required.", nameof(groupId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "A name/description translation needs at least a name or a description.", nameof(name));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new KeyNotFoundException($"Group '{groupId}' was not found in the session; nothing to translate.");

        var via = ResolveGroupTranslationStanding(group.OwnerId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the group's owner (or a translator, or an admin) " +
                "may add a translation of its name or description.");

        var now = DateTimeOffset.UtcNow;
        var translation = new GroupTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            GroupId = groupId,
            LanguageCode = languageCode,
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            AuthorId = actorId,
            Created = now
        };

        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "grouptranslation.add",
            TargetKind = "group",
            TargetId = groupId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(translation);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return translation;
    }

    /// <inheritdoc />
    public bool CanTranslateGroup(string ownerId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveGroupTranslationStanding(ownerId, actorId, actorRoles) is not null;

    // ─── ADR 0048 — edit + delete lane for group / community translations ─
    // ADR 0026 was add-only (one row per (parent, language) pair). ADR 0048
    // lifts the "add-only" pin: the same standing matrix (group owner /
    // GlobalAdmin / Translator for a group; GlobalAdmin / Translator for the
    // community — no owner on the component) may now **update** the existing
    // (parent, languageCode) row in place, or **remove** it. Standing is
    // re-derived from the parent's current owner/scope, not from the row's
    // AuthorId (which is the adder). Both write a hand-written
    // <c>AccessAudit</c> row and commit atomically (one <c>SaveChangesAsync</c>).

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
        if (string.IsNullOrEmpty(groupId))
            throw new ArgumentException("A group id is required.", nameof(groupId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A group translation requires a name or a description (at least one non-blank).", nameof(name));

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new KeyNotFoundException($"Group '{groupId}' was not found in the session; nothing to update.");

        var row = await session.Query<GroupTranslation>()
            .Where(t => t.GroupId == groupId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Group '{groupId}' has no translation in '{languageCode}'; nothing to update.");

        var via = ResolveGroupTranslationStanding(group.OwnerId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the group owner, an admin, or a translator may edit a group's translation.");

        row.Name = string.IsNullOrWhiteSpace(name) ? null : name;
        row.Description = string.IsNullOrWhiteSpace(description) ? null : description;

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "grouptranslation.update",
            TargetKind = "group",
            TargetId = groupId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
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
        if (string.IsNullOrEmpty(groupId))
            throw new ArgumentException("A group id is required.", nameof(groupId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var group = await session.LoadAsync<Group>(groupId).ConfigureAwait(false);
        if (group is null)
            throw new KeyNotFoundException($"Group '{groupId}' was not found in the session; nothing to remove.");

        var row = await session.Query<GroupTranslation>()
            .Where(t => t.GroupId == groupId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Group '{groupId}' has no translation in '{languageCode}'; nothing to remove.");

        var via = ResolveGroupTranslationStanding(group.OwnerId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the group owner, an admin, or a translator may remove a group's translation.");

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "grouptranslation.remove",
            TargetKind = "group",
            TargetId = groupId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Delete(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
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

        // ADR 0083 — notify the invited resident (kind group.invite; the
        // recipient is <c>userId</c>, not <c>invitedBy</c>). Same shape as
        // the <c>AddGroupMemberAsync</c> emitter: the emission sits in this
        // session's transaction, the <c>NotificationService</c> is resolved
        // lazily from <c>IServiceProvider</c> (see the class doc-comment for
        // the circular-dependency rationale), and pre-ADR 0083 direct-
        // construction test harnesses (services = null) silently skip.
        var ns = services?.GetService<Notifications.NotificationService>();
        if (ns is not null)
        {
            // ADR 0095 — the group-invite notification now carries the accept /
            // decline actions: the invitee can act on the email's links (or the
            // inbox's buttons) without navigating to the group first. The two
            // same-origin relative paths point at the Web's self-lane GET
            // actions (GroupsController.AcceptInvitationLink / DeclineInvitationLink,
            // the same [Authorize] gate as the POST self-lane); the
            // NotificationService stores them relative and appends them to the
            // email as BaseUrl-prefixed absolute links (the VerificationOptions.BaseUrl
            // precedent). The idempotency key / body / kind are unchanged (ADR
            // 0083's emission shape — this is an additive ADR 0095 lane, not a
            // re-emission; a re-invite still dedups on the same key).
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

    // ── ADR 0094 — resident self-initiated join requests for public groups ──
    // The reverse direction of the m2b owner-invited lane: the *resident*
    // starts it (request + withdraw self-lane) and the group's owner ∪
    // GlobalAdmin resolves it (approve → membership lands / decline). Same
    // document conventions (business-key upsert, C3 audit in-session, C4
    // strong consistency on approve), the same GU supervised-child wall on the
    // membership-landing lane (approve), and the same "a resolved row is an
    // invalid transition" state machine.

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

    // ── ADR 0094 read lanes (candidate reads — no AccessAudit row, C-M2·2
    // carried; live rows, invariant C4) ─────────────────────────────────

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
    public async Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy)
    {
        // C-MED·8 single write lane (design doc §2.2): point Profile.AvatarId at a
        // MediaObject (a content hash) or clear it when avatarId is null. The
        // owner-scope check happens at the Web boundary (the self-only upload
        // lane) — this lane writes Profile.AvatarId only. One session, one
        // SaveChangesAsync (C3); no audit row (a Profile field write — the
        // UpsertProfileAsync shape, "not an access decision"; the serving lane's
        // audit row is owned by the caller's CanAsync gate, C-MED·2). Fail closed
        // on a missing profile (design doc §2.2).
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.AvatarId = avatarId;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileTimezoneAsync(string subjectId, string? timezone, string actorBy)
    {
        // ADR 0019 — the user's timezone override write lane. Mirrors
        // SetProfileAvatarAsync exactly (the C-MED·8 single write-lane shape):
        // the self-scope check happens at the Web boundary (the owner is the
        // actor); this lane writes Profile.TimeZone only. One session, one
        // SaveChangesAsync (C3); no audit row (a Profile field write — the
        // UpsertProfileAsync shape, "not an access decision"). Fail closed on a
        // missing profile (never load-or-create, the SetProfileAvatarAsync pin).
        // Strong consistency (C4): the new value is live on the very next
        // GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.TimeZone = timezone;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileDateFormatAsync(string subjectId, string? formatString, string actorBy)
    {
        // ADR 0020 — the user's date-format override write lane. Mirrors
        // SetProfileTimezoneAsync exactly (the C-MED·8 single write-lane shape):
        // the self-scope check happens at the Web boundary (the owner is the
        // actor); this lane writes Profile.DateFormat only. One session, one
        // SaveChangesAsync (C3); no audit row (a Profile field write — the
        // UpsertProfileAsync shape, "not an access decision"). Fail closed on a
        // missing profile (never load-or-create, the SetProfileTimezoneAsync
        // pin). Strong consistency (C4): the new value is live on the very next
        // GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.DateFormat = formatString;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileEmailLanguageAsync(string subjectId, string? emailLanguage, string actorBy)
    {
        // ADR 0061 — the user's outbound-channel language write lane. Mirrors
        // SetProfileDateFormatAsync exactly (the C-MED·8 single write-lane
        // shape): the self-scope check happens at the Web boundary (the owner
        // is the actor); this lane writes Profile.EmailLanguage only. One
        // session, one SaveChangesAsync (C3); no audit row (a Profile field
        // write — the UpsertProfileAsync shape, "not an access decision").
        // Fail closed on a missing profile (never load-or-create, the
        // SetProfileDateFormatAsync pin). Strong consistency (C4): the new
        // value is live on the very next GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.EmailLanguage = emailLanguage;
        session.Store(profile);
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
        var explicitMembership = await session
            .Query<ComponentMembership>()
            .Where(m => m.UserId == userId)
            .Select(m => m.ComponentId)
            .ToListAsync()
            .ConfigureAwait(false);

        // ADR 0012: mandatory communities — every verified resident is a
        // member, so the union below is the single "who is a member"
        // definition (posting gate, composer picker, feed directory all read
        // through it — a flag toggle is live on the very next read, C4).
        // Enabled ∩ mandatory: a disabled component is the user-chosen
        // "removed" shape and grants no membership at all.
        var mandatory = await session
            .Query<Component>()
            .Where(c => c.Enabled && c.Mandatory)
            .Select(c => c.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var union = new HashSet<string>(explicitMembership);
        union.UnionWith(mandatory);
        return union;
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

        // ADR 0012: a mandatory community's membership is implicit — the
        // GetCommunityIdsAsync union would return the pair no matter which
        // row we clear, so "clearing" it changes nothing. No-op skip (not an
        // error — the /admin diff form and the self-leave route may
        // legitimately reach this lane with a mandatory pair, and nothing is
        // removed there), and no audit row (a no-op writes nothing). The
        // *refused* shape lives in RemoveCommunityMemberAsync (the
        // moderator lane that wants the surfaced product message).
        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is not null && component.Mandatory)
            return;

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

    // ── ADR 0012 — mandatory communities + moderator member lanes ─────────
    // Thin token, fat authorization: these lanes gate on the caller's current
    // role set (the <c>actorRoles</c> seam — the same <c>IReadOnlySet&lt;string&gt;</c>
    // shape PostService.CreatePostAsync / AnnouncementService.CreateAsync take,
    // minted at the Web boundary from KumunitaPrincipal.RoleSet), and the
    // decision is made here in Core: the **set-mandatory lane is GlobalAdmin-only**
    // (a standing-wide decision the product puts in the admin's hands), while
    // the member add/remove lanes carry Roles.GlobalAdmin ∪ the community's
    // Roles.ModeratorComponent scope. Writes audit in the same session (C3)
    // with the narrower standing recorded — a claim-holder acting through
    // their community scope records <c>Via: Moderator</c>; a GlobalAdmin
    // records <c>Via: Admin</c>.

    /// <inheritdoc />
    public async Task SetCommunityMandatoryAsync(string componentId, bool mandatory, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));

        GateCommunityGlobalAdmin(componentId, actorId, actorRoles);
        var via = Authorization.AccessVia.Admin;
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        component.Mandatory = mandatory;
        session.Store(component);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = mandatory ? "community.set-mandatory" : "community.set-optional",
            TargetKind = "component",
            TargetId = componentId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    // ── ADR 0026 — community name/description translations ─────────────────
    // Mirrors the group lane above, in the community shape: a
    // CommunityTranslation row (at most one per language, the M1DocTypes unique
    // index) added by a GlobalAdmin or a Translator (both Via: Admin — a
    // community has no owner, so no AccessVia.Owner branch; a component
    // moderator governs its members, ADR 0012, not its name), a hand-written
    // AccessAudit row in the same session (C3), a plain read seam (no decision,
    // no audit — inherits the community's enabled visibility). Add-only.

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommunityTranslation>> GetCommunityTranslationsAsync(string componentId)
    {
        if (string.IsNullOrEmpty(componentId)) throw new ArgumentException("A component id is required.", nameof(componentId));

        await using var session = store.QuerySession();
        return await session
            .Query<CommunityTranslation>()
            .Where(t => t.ComponentId == componentId)
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CommunityTranslation> AddCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session)
    {
        if (string.IsNullOrEmpty(componentId)) throw new ArgumentException("A component id is required.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "A name/description translation needs at least a name or a description.", nameof(name));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new KeyNotFoundException($"Community '{componentId}' was not found in the session; nothing to translate.");

        var via = ResolveCommunityTranslationStanding(actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only a translator (or an admin) may add a translation of " +
                "the community's name or description.");

        var now = DateTimeOffset.UtcNow;
        var translation = new CommunityTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            ComponentId = componentId,
            LanguageCode = languageCode,
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            AuthorId = actorId,
            Created = now
        };

        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "communitytranslation.add",
            TargetKind = "component",
            TargetId = componentId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(translation);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return translation;
    }

    /// <summary>
    /// **Updates** the existing <see cref="CommunityTranslation"/> row for
    /// (<paramref name="componentId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing is the same as the ADR
    /// 0026 add lane (GlobalAdmin / Translator → Admin; the community has no
    /// owner). A missing component or row is a <see cref="KeyNotFoundException"/>;
    /// a denied actor throws <see cref="UnauthorizedAccessException"/>. The new
    /// name / description must have at least one non-blank field. One
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task<CommunityTranslation> UpdateCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(componentId))
            throw new ArgumentException("A component id is required.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "A name/description translation needs at least a name or a description.", nameof(name));

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new KeyNotFoundException($"Community '{componentId}' was not found in the session; nothing to update.");

        var row = await session.Query<CommunityTranslation>()
            .Where(t => t.ComponentId == componentId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"Community '{componentId}' has no translation in '{languageCode}'; nothing to update.");

        var via = ResolveCommunityTranslationStanding(actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only a translator (or an admin) may edit a translation of " +
                "the community's name or description.");

        row.Name = string.IsNullOrWhiteSpace(name) ? null : name;
        row.Description = string.IsNullOrWhiteSpace(description) ? null : description;

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "communitytranslation.update",
            TargetKind = "component",
            TargetId = componentId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <summary>
    /// **Removes** the existing <see cref="CommunityTranslation"/> row for
    /// (<paramref name="componentId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing is the same as the ADR
    /// 0026 add lane. A hard <c>session.Delete</c>; the trail is preserved by
    /// the <see cref="Authorization.AccessAudit"/> row (action
    /// <c>communitytranslation.remove</c>). A missing component or row is a
    /// <see cref="KeyNotFoundException"/>. One <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task RemoveCommunityTranslationAsync(
        string componentId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(componentId))
            throw new ArgumentException("A component id is required.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new KeyNotFoundException($"Community '{componentId}' was not found in the session; nothing to remove.");

        var row = await session.Query<CommunityTranslation>()
            .Where(t => t.ComponentId == componentId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"Community '{componentId}' has no translation in '{languageCode}'; nothing to remove.");

        var via = ResolveCommunityTranslationStanding(actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only a translator (or an admin) may remove a translation of " +
                "the community's name or description.");

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "communitytranslation.remove",
            TargetKind = "component",
            TargetId = componentId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Delete(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool CanTranslateCommunity(string actorId, IReadOnlySet<string> actorRoles)
        => ResolveCommunityTranslationStanding(actorId, actorRoles) is not null;

    /// <summary>
    /// The ADR 0026 community-translation standing resolver (shared by
    /// <see cref="AddCommunityTranslationAsync"/> and <see
    /// cref="CanTranslateCommunity"/>). Returns the <see cref="Authorization
    /// .AccessVia"/> the actor qualifies under, or <c>null</c> to deny. A
    /// community has **no owner** (no <see cref="Authorization.AccessVia
    /// .Owner"/> branch) — its name is a GlobalAdmin artifact (the
    /// <c>/admin</c> create/update lane, <c>Via: Admin</c>) — so the admitted
    /// standings are a <b>GlobalAdmin</b> or a <b>Translator</b> (both
    /// <see cref="Authorization.AccessVia.Admin"/>). A component-moderator is
    /// not (a moderator governs a community's *members*, ADR 0012, not its
    /// name).
    /// </summary>
    private static Authorization.AccessVia? ResolveCommunityTranslationStanding(
        string actorId, IReadOnlySet<string> actorRoles)
    {
        if (actorRoles.Contains(Identity.Roles.GlobalAdmin))
            return Authorization.AccessVia.Admin;
        if (actorRoles.Contains(Identity.Roles.Translator))
            return Authorization.AccessVia.Admin;
        return null;
    }

    /// <inheritdoc />
    public async Task AddCommunityMemberAsync(string componentId, string userId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", nameof(userId));

        // GU (ADR 0028): the guardian base is the narrowest and **bypasses** the
        // community standing gate — a guardian holds neither GlobalAdmin nor a
        // moderator scope by definition, so it must resolve before the gate is
        // called. An active link over <paramref name="userId"/> (the child)
        // records <c>Via: Guardian</c>; otherwise the existing gate applies.
        var via = await GateGuardianStandingAsync(actorId, userId)
            ?? GateCommunityStanding(componentId, actorId, actorRoles);
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // The component must exist (a membership on a missing component is a data
        // bug, not a no-op — the frozen admin lane's shape, kept).
        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        // Same idempotent business-key upsert as SetCommunityMembershipAsync
        // (the M1DocTypes unique index enforces one row per pair). A row on a
        // mandatory community is a harmless no-op — the union read already
        // includes the resident (kept so the forms round-trip with one lane).
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
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveCommunityMemberAsync(string componentId, string userId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", nameof(userId));

        // GU (ADR 0028): the guardian base is the narrowest and **bypasses** the
        // community standing gate — a guardian holds neither GlobalAdmin nor a
        // moderator scope by definition, so it must resolve before the gate is
        // called. An active link over <paramref name="userId"/> (the child)
        // records <c>Via: Guardian</c>; otherwise the existing gate applies.
        // The mandatory-community refusal below still fires after the <c>via</c>
        // resolution (unchanged order — the branch adds a path, never removes one).
        var via = await GateGuardianStandingAsync(actorId, userId)
            ?? GateCommunityStanding(componentId, actorId, actorRoles);
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        // ADR 0012's invariant: no one is a non-member of a mandatory
        // community. The union read (GetCommunityIdsAsync) would include
        // <paramref name="userId"/> no matter which row we deleted, so a
        // removal here is a refusal — not a no-op (the Clear lane skips
        // because the /admin diff form may legitimately reach that pair;
        // this lane is where the product message surfaces).
        if (component.Mandatory)
            throw new InvalidOperationException(
                $"Community \"{component.Name}\" is mandatory — residency in it is implicit and cannot be removed. Mark it optional first.");

        // Delete (if any) — strong consistency (invariant C4): the union read
        // drops the pair on the very next call; an absent row is a no-op
        // (the admin-set-form shape, consistent with the frozen lane).
        var membership = await session.Query<ComponentMembership>()
            .Where(m => m.ComponentId == componentId && m.UserId == userId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (membership is not null)
            session.Delete(membership);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "community.remove-member",
            TargetKind = "component",
            TargetId = componentId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ComponentMembership>> GetCommunityMembersAsync(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));

        // Live-row read (invariant C4), the GetGroupMembersAsync analog on
        // the component axis: the explicit membership rows of one Component,
        // as an access decision. No audit row (a read, not a decision).
        await using var session = store.QuerySession();
        return await session
            .Query<ComponentMembership>()
            .Where(m => m.ComponentId == componentId)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The ADR 0012 standing gate for the community-management lanes: the
    /// caller's current role set (their <c>actorRoles</c> argument — never a
    /// re-derivation from the database; Core decides from the standing the
    /// principal mints) must carry <see cref="Identity.Roles.GlobalAdmin"/>
    /// ∪ the community's <see cref="Identity.Roles
    /// .ModeratorComponent(string)"/> scope claim —
    /// <see cref="UnauthorizedAccessException"/> otherwise (fail-closed: a
    /// null/empty set holds neither). Also resolves the <b>recorded</b>
    /// standing: the narrower scope wins — a claim-holder acting
    /// through their community scope records via <c>Moderator</c> (M3B's
    /// "Via: Moderator when scoped" shape), a pure GlobalAdmin via <c>Admin</c>.
    /// </summary>
    private static Authorization.AccessVia GateCommunityStanding(
        string componentId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        var isGlobalAdmin = actorRoles is not null
            && actorRoles.Contains(Identity.Roles.GlobalAdmin);
        var isComponentModerator = actorRoles is not null
            && actorRoles.Contains(Identity.Roles.ModeratorComponent(componentId));

        if (!isGlobalAdmin && !isComponentModerator)
            throw new UnauthorizedAccessException(
                $"Account {actorId} does not hold GlobalAdmin or the moderator scope for community {componentId}.");

        return isComponentModerator
            ? Authorization.AccessVia.Moderator
            : Authorization.AccessVia.Admin;
    }

    /// <summary>
    /// The GU (ADR 0028) standing basis for the membership-curation lanes:
    /// the actor holds an <b>active</b> <see cref="GuardianLink"/> with
    /// <c>GuardianId == actor</c> and <c>ChildId == child</c> — i.e. the
    /// actor is curating their own child's membership. Returns
    /// <see cref="Authorization.AccessVia.Guardian"/> when that holds, else
    /// <c>null</c> (the lane's existing base — Owner / Moderator / Admin —
    /// then applies). Narrower-standing record (G·3): a guardian acting for
    /// their child records <c>Guardian</c>, not a broader role they also hold.
    /// Read-only — no session mutation.
    /// </summary>
    private async Task<Authorization.AccessVia?> GateGuardianStandingAsync(
        string actorId, string childId)
    {
        if (string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(childId))
            return null;

        await using var session = store.QuerySession();
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == actorId && l.ChildId == childId
                        && l.Status == GuardianLinkStatus.Active)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        return link is not null ? Authorization.AccessVia.Guardian : null;
    }

    /// <summary>
    /// The stricter ADR 0012 gate on <b>set-mandatory only</b> (the flag is
    /// a standing-wide decision the product puts in the admin's hands): the
    /// caller's role set must carry <see cref="Identity.Roles.GlobalAdmin"/> —
    /// <see cref="UnauthorizedAccessException"/> otherwise, including for a
    /// community moderator's scope claim alone.
    /// </summary>
    private static void GateCommunityGlobalAdmin(string componentId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        if (actorRoles is null || !actorRoles.Contains(Identity.Roles.GlobalAdmin))
            throw new UnauthorizedAccessException(
                $"Account {actorId} does not hold GlobalAdmin (required to change the mandatory state of community {componentId}).");
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

    // ── GU guardian lanes (ADR 0028) — additive, beside the membership lanes ──
    // Account-scope supervision of a child's account. Standing is the 9th
    // <c>AccessVia.Guardian</c> value, resolved **live** off the active
    // <see cref="GuardianLink"/> row (G·2) and **deny-by-default** (G·3). These
    // are management lanes — **never** on a CanAsync / CanSeeAsync content
    // decision path (G·1, the load-bearing honesty). Exception vocabulary:
    // <c>UnauthorizedAccessException</c> = the actor has no standing (no active
    // link, or not the GuardianId); <c>InvalidOperationException</c> = a row is
    // missing or in a bad state.

    /// <inheritdoc />
    public async Task<GuardianLink> CreateGuardianLinkAsync(string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // G·4 idempotent formation: a duplicate (GuardianId, ChildId) Active row
        // is a no-op — the row is left as-is and returned (not a throw).
        var existing = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // No mutation, no audit (a no-op is a no-op — the contract, not an
            // error). Return the existing row as-is.
            return existing;
        }

        var link = new GuardianLink
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianId = guardianId,
            ChildId = childId,
            Status = GuardianLinkStatus.Active,
            CreatedAt = now
        };
        session.Store(link);

        // Formation audit: the guardian's own standing (all three identities the
        // guardian; the target is the guardian-link row itself, keyed to the
        // child). One SaveChangesAsync (invariant C3).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.create",
            TargetKind = "guardian-link",
            TargetId = link.Id,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return link;
    }

    /// <inheritdoc />
    public async Task<GuardianLink> AssignGuardianLinkAsync(
        string childId, string guardianId, string assignedById)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));
        if (string.IsNullOrWhiteSpace(assignedById))
            throw new ArgumentException("Assigning guardian id is required.", nameof(assignedById));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // S·6 — idempotent formation: a duplicate (GuardianId, ChildId) Active row
        // is a no-op — the row is left as-is and returned (not a throw), no
        // second pair of audit rows (the G-A·4 precedent, inherited).
        var existing = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // No mutation, no audit (a no-op is a no-op — the contract, not an
            // error). Return the existing row as-is.
            return existing;
        }

        var link = new GuardianLink
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianId = guardianId,
            ChildId = childId,
            Status = GuardianLinkStatus.Active,
            CreatedAt = now
        };
        session.Store(link);

        // S·5 — the two complementary audit rows, written in the SAME session
        // (S·1 — one SaveChangesAsync, no partial write):
        //
        // (1) guardian.create — the GU seam's shape, byte-identical to what
        //     CreateGuardianLinkAsync writes (S·2): ActorId = the ASSIGNED
        //     guardian (the standing-holder).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.create",
            TargetKind = "guardian-link",
            TargetId = link.Id,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        // (2) guardian.assign — the GA-AR conferral verb: ActorId = the
        //     ASSIGNING guardian (the conferrer, S·5).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = assignedById,
            EffectivePrincipalId = assignedById,
            Action = "guardian.assign",
            TargetKind = "guardian-link",
            TargetId = link.Id,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return link;
    }

    /// <inheritdoc />
    public async Task SuspendChildAsync(string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        // Load the child's profile (missing → bad state, not a no-op).
        var profile = await session.Query<Profile>()
            .Where(p => p.SubjectId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (profile is null)
            throw new InvalidOperationException($"No profile for child {childId}.");

        // Set the SAME flag BlockedAccountMiddleware + the directory already read
        // (enforcement parity — the U01 pin).
        profile.Blocked = true;
        session.Store(profile);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.suspend",
            TargetKind = "profile",
            TargetId = childId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UnsuspendChildAsync(string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        var profile = await session.Query<Profile>()
            .Where(p => p.SubjectId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (profile is null)
            throw new InvalidOperationException($"No profile for child {childId}.");

        // Restore standing — live on the next read (G·2).
        profile.Blocked = false;
        session.Store(profile);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.unsuspend",
            TargetKind = "profile",
            TargetId = childId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DissolveGuardianLinkAsync(string linkId, string actorId, bool viaAdmin)
    {
        if (string.IsNullOrWhiteSpace(linkId))
            throw new ArgumentException("Link id is required.", nameof(linkId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var link = await session.LoadAsync<GuardianLink>(linkId).ConfigureAwait(false);
        if (link is null)
            throw new InvalidOperationException($"No guardian link: {linkId}");

        // G·4: on the guardian's own lane the actor must BE the GuardianId
        // (deny-by-default). The G·5 safety valve (viaAdmin) skips the check.
        if (!viaAdmin && link.GuardianId != actorId)
            throw new UnauthorizedAccessException(
                $"Account {actorId} is not the guardian on link {linkId}.");

        link.Status = GuardianLinkStatus.Dissolved;
        link.DissolvedAt = now;
        link.DissolvedBy = actorId;
        session.Store(link);

        // A dissolve writes NOTHING to membership and does NOT set
        // Profile.Blocked — the self-lanes restore on the next read (G·2/C4);
        // un-suspend is a separate act (the G·5 valve or UnsuspendChildAsync).
        var via = viaAdmin ? Authorization.AccessVia.Admin : Authorization.AccessVia.Guardian;

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "guardian.dissolve",
            TargetKind = "guardian-link",
            TargetId = linkId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
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
    private static async Task GuardActiveLinkAsync(
        IDocumentSession session, string guardianId, string childId)
    {
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId
                        && l.Status == GuardianLinkStatus.Active)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (link is null)
            throw new UnauthorizedAccessException(
                $"No active guardian link for ({guardianId}, {childId}).");
    }
}
