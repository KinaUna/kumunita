using Kumunita.Core.Notifications;
using Kumunita.Core.Translations;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// Concrete <see cref="IUserInfoService"/> (M1 step 4): the UserInfoModule's access-core
/// surface (ADR 0006 §A frozen + ADR 0286 community lanes). The 71 non-frozen
/// methods split into four siblings (C07): <see cref="GroupService"/>,
/// <see cref="CommunityMembershipService"/>, <see cref="GuardianService"/>,
/// <see cref="ProfileSettingsService"/>.
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
    private readonly UserInfoStore _shared = new(store, services);

    // C06 (ADR 0288) — the group / community content contract, shared by the
    // two descriptors: at least one of (name, description) must be non-blank —
    // a name/description translation with neither is not a translation.

    // ── Read paths (plan step 3 — live-row reads, invariant C4) ───────────

    /// <inheritdoc />
    public async Task<Profile?> GetProfileAsync(string subjectId)
    {
        // Single document read — Profile's identity is SubjectId (pinned in
        // CoreDocTypes.Configure), so the equality probe lands on the document id.
        await using var session = store.QuerySession();
        return await session
            .Query<Profile>()
            .Where(p => p.SubjectId == subjectId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
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
        var ns = services?.GetService<INotificationEmitter>();
        if (ns is not null)
        {
            await ns.EmitAsync(
                session,
                userId,
                Notifications.NotificationKinds.GroupAdded,
                $"notification:{Notifications.NotificationKinds.GroupAdded}:{groupId}:{userId}",
                group.Name,
                // IMPROVE-UX R09 (E14): the inbox row lands the resident on the
                // group. `targetId` enables the ADR 0084 per-target subscription
                // gate (opt-out kind → enabled by default, so the row is still
                // stored); `linkPath` is stored on the row + rendered as the
                // clickable destination. `ct:` must stay **named** (a positional
                // token here would bind to the acceptPath/declinePath slots).
                targetId: groupId,
                linkPath: $"/groups/{groupId}",
                ct: CancellationToken.None).ConfigureAwait(false);
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

    // ── ADR 0026 — group name/description translations ─────────────────────
    // Mirrors the ADR 0022 post-translation lane (PostService), in this context:
    // a GroupTranslation row (at most one per language, the CoreDocTypes unique
    // index) added by the group's owner (Via: Owner) or a GlobalAdmin /
    // Translator (Via: Admin), a hand-written AccessAudit row in the same
    // session (C3), and a plain read seam (no decision, no audit — inherits the
    // group's owner∪member reach). Add-only (no edit/delete seam).

    // ─── ADR 0048 — edit + delete lane for group / community translations ─
    // ADR 0026 was add-only (one row per (parent, language) pair). ADR 0048
    // lifts the "add-only" pin: the same standing matrix (group owner /
    // GlobalAdmin / Translator for a group; GlobalAdmin / Translator for the
    // community — no owner on the component) may now **update** the existing
    // (parent, languageCode) row in place, or **remove** it. Standing is
    // re-derived from the parent's current owner/scope, not from the row's
    // AuthorId (which is the adder). Both write a hand-written
    // <c>AccessAudit</c> row and commit atomically (one <c>SaveChangesAsync</c>).

    // ── M2b: group invitations (docs/design/m2b-group-invitations.md;
    // one session + one SaveChangesAsync per call, mirroring the M1 group
    // lifecycle shape above — invariants C-M2b·1..3) ──────────────────

    // ── GU community-approval lane — the sibling of the m2b group-invitation
    // lane, but for communities and with the guardian as the resolver ─────

    // ── M2b read lanes (candidate reads — no AccessAudit row, C-M2·2
    // carried; live rows, invariant C4) ─────────────────────────────────

    // ── ADR 0094 — resident self-initiated join requests for public groups ──
    // The reverse direction of the m2b owner-invited lane: the *resident*
    // starts it (request + withdraw self-lane) and the group's owner ∪
    // GlobalAdmin resolves it (approve → membership lands / decline). Same
    // document conventions (business-key upsert, C3 audit in-session, C4
    // strong consistency on approve), the same GU supervised-child wall on the
    // membership-landing lane (approve), and the same "a resolved row is an
    // invalid transition" state machine.

    // ── ADR 0094 read lanes (candidate reads — no AccessAudit row, C-M2·2
    // carried; live rows, invariant C4) ─────────────────────────────────

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

    // ── Admin community management (add / edit / enable-disable, GlobalAdmin
    // surface) — the same admin-action audit lane as the methods above: the
    // <see cref="Authorization.AccessAudit"/> row (via: Admin) and the domain
    // row commit atomically in one session (invariant C3). ──────────────

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
    public async Task<IReadOnlyCollection<string>> GetEffectiveCommunityIdsAsync(string userId)
    {
        // The child's own access surface reads through this: the raw
        // membership (explicit ∪ enabled ∩ mandatory) MINUS the guardian's
        // per-community block (Profile.BlockedCommunityIds). A mandatory
        // community that a guardian has blocked is still an implicit member in
        // GetCommunityIdsAsync (the curation/admin raw read), but drops out of
        // THIS effective set — which is exactly what hides it from the child's
        // directory, feed, posting gate, and audience visibility (ADR 0012's
        // "cannot remove" is bypassed by "block", the MessagingRestricted
        // ceiling precedent carried to communities).
        if (string.IsNullOrWhiteSpace(userId))
            return System.Array.Empty<string>();

        var effective = new HashSet<string>(await GetCommunityIdsAsync(userId).ConfigureAwait(false), StringComparer.Ordinal);

        // A missing profile degrades to the raw membership (the floor) — the
        // MessagingRestricted null-safe shape.
        await using var session = store.QuerySession();
        var profile = await session.LoadAsync<Profile>(userId).ConfigureAwait(false);
        if (profile is not null && profile.BlockedCommunityIds is { Count: > 0 })
        {
            foreach (var blockedId in profile.BlockedCommunityIds)
                effective.Remove(blockedId);
        }

        return effective;
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

        // GU community-approval lane — the supervised-child branch (the
        // <see cref="AddCommunityMemberAsync"/> / ADR 0094 / m2b
        // shape): a pending <see cref="CommunityMembershipRequest"/> row
        // lands (the membership does not), the guardians get the
        // <c>guardian.community_invite</c> notification, and the
        // <c>community.membership.request</c> audit row commits in the
        // same transaction (invariant C3).
        // GU (ADR 0028) — the guardian's own curation lane (the
        // <see cref="AddGroupMemberAsync"/> precedent): when the actor is
        // one of the child's active guardians, the membership lands
        // immediately with <c>Via: Guardian</c> — no pending request, no
        // notification fan-out (the actor is a recipient, and a
        // self-notification would be noise).
        var activeGuardians = await _shared.ActiveGuardianIdsAsync(session, userId).ConfigureAwait(false);
        var isGuardianSelfCuration = activeGuardians.Contains(actorId);
        if (activeGuardians.Count > 0 && !isGuardianSelfCuration)
        {
            await StoreCommunityRequestForSupervisedChildAsync(
                session, component, componentId, userId, actorId, now,
                Authorization.AccessVia.Admin, activeGuardians).ConfigureAwait(false);
            await session.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

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
    // CommunityTranslation row (at most one per language, the CoreDocTypes unique
    // index) added by a GlobalAdmin or a Translator (both Via: Admin — a
    // community has no owner, so no AccessVia.Owner branch; a component
    // moderator governs its members, ADR 0012, not its name), a hand-written
    // AccessAudit row in the same session (C3), a plain read seam (no decision,
    // no audit — inherits the community's enabled visibility). Add-only.

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

        // GU community-approval lane — the supervised-child branch: when the
        // add target holds an ACTIVE <see cref="GuardianLink"/>, the
        // <b>membership does not land</b>. A pending
        // <see cref="CommunityMembershipRequest"/> row lands instead (the
        // ADR 0094 / m2b state-machine shape: the membership fact itself
        // never arrives until the guardian resolves via
        // <see cref="ApproveCommunityMembershipRequestAsync"/>), and the
        // guardians get the <c>guardian.community_invite</c> notification
        // (one row per active guardian; the
        // <see cref="EmitGuardianNotificationAsync"/> shape with
        // LinkPath / AcceptPath / DeclinePath all pointing at
        // <c>/me/children/{childId}</c>, the manage-child page). The
        // auditor's <c>community.membership.request</c> audit row commits
        // in the same transaction (invariant C3 — the pending row, the
        // audit, and the notification inbox rows are atomic).
        //
        // GU (ADR 0028) — the guardian's own curation lane (the
        // <see cref="AddGroupMemberAsync"/> precedent): when the actor is
        // one of the child's active guardians, the membership lands
        // immediately with <c>Via: Guardian</c> — no pending request, no
        // notification fan-out (the actor is a recipient, and a
        // self-notification would be noise).
        var activeGuardians = await _shared.ActiveGuardianIdsAsync(session, userId).ConfigureAwait(false);
        var isGuardianSelfCuration = activeGuardians.Contains(actorId);
        if (activeGuardians.Count > 0 && !isGuardianSelfCuration)
        {
            await StoreCommunityRequestForSupervisedChildAsync(
                session, component, componentId, userId, actorId, now,
                via, activeGuardians).ConfigureAwait(false);
            await session.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        // Same idempotent business-key upsert as SetCommunityMembershipAsync
        // (the CoreDocTypes unique index enforces one row per pair). A row on a
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

    // ── GU guardian lanes (ADR 0028) — additive, beside the membership lanes ──
    // Account-scope supervision of a child's account. Standing is the 9th
    // <c>AccessVia.Guardian</c> value, resolved **live** off the active
    // <see cref="GuardianLink"/> row (G·2) and **deny-by-default** (G·3). These
    // are management lanes — **never** on a CanAsync / CanSeeAsync content
    // decision path (G·1, the load-bearing honesty). Exception vocabulary:
    // <c>UnauthorizedAccessException</c> = the actor has no standing (no active
    // link, or not the GuardianId); <c>InvalidOperationException</c> = a row is
    // missing or in a bad state.

    // ── M28 (ADR 0151, D4) — guardian time limits: the three seams ─────────
    // The inverse of the M20 quiet lane (ADR 0121) on the GU surface (ADR
    // 0028). Gate = an active <see cref="GuardianLink"/> for (guardian, child)
    // ∪ GlobalAdmin (G·2 live, G·5 safety valve); a non-guardian, non-admin —
    // including a child acting on their own <c>childId</c> (C-M28·7) — is
    // denied. The write appends exactly one <see
    // cref="Authorization.AccessAudit"/> row (verb
    // <c>guardian.time-limit.set</c>, <c>Via</c> Guardian/Admin); the two
    // reads append none. Zero new authorization surface (C-M28·5).

    /// <summary>
    /// The GU community-approval lane's shared write for the supervised-child
    /// branch of <see cref="AddCommunityMemberAsync"/> /
    /// <see cref="SetCommunityMembershipAsync"/>: a pending
    /// <see cref="CommunityMembershipRequest"/> row (the
    /// <see cref="CommunityMembership"/> itself does NOT land — the
    /// <see cref="ApproveCommunityMembershipRequestAsync"/> approve lane
    /// writes it, the ADR 0094 / m2b state-machine shape), the
    /// <c>community.membership.request</c> audit row, and the
    /// <c>guardian.community_invite</c> notification fan-out (one inbox row
    /// per active guardian). The caller's transaction commits — one
    /// <c>SaveChangesAsync</c> (invariant C3: the pending row + the audit
    /// row + the inbox rows are atomic).
    /// </summary>
    private async Task StoreCommunityRequestForSupervisedChildAsync(
        IDocumentSession session,
        Component component,
        string componentId,
        string childId,
        string actorId,
        DateTimeOffset now,
        Authorization.AccessVia via,
        IReadOnlyCollection<string> activeGuardians)
    {
        var row = await session.Query<CommunityMembershipRequest>()
            .Where(r => r.ComponentId == componentId && r.UserId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new CommunityMembershipRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                ComponentId = componentId,
                UserId = childId,
                RequestedBy = actorId,
                Status = CommunityMembershipRequestStatus.Pending,
                RequestedAt = now
            };
        }
        else
        {
            // Re-request (the m2b C-M2b·3 reset shape): a resolved row (or an
            // already-Pending re-stamp) resets to the fresh Pending shape —
            // the two resolve stamps are cleared with it.
            row.RequestedBy = actorId;
            row.Status = CommunityMembershipRequestStatus.Pending;
            row.RequestedAt = now;
            row.ResolvedAt = null;
            row.ResolvedBy = null;
        }

        session.Store(row);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "community.membership.request",
            TargetKind = "component",
            TargetId = componentId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        var childName = await _shared.ChildDisplayNameAsync(childId).ConfigureAwait(false);
        foreach (var guardianId in activeGuardians)
        {
            await _shared.EmitGuardianNotificationAsync(
                session, childId, guardianId,
                Notifications.NotificationKinds.GuardianCommunityInvite,
                componentId,
                $"{component.Name} — {childName}").ConfigureAwait(false);
        }
    }
}