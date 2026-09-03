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
            Phone = profile.Phone
        };

        // Patch wins on every non-null field; a null field leaves the current value untouched.
        if (patch.DisplayName is not null) doc.DisplayName = patch.DisplayName;
        if (patch.Email is not null) doc.Email = patch.Email;
        if (patch.Phone is not null) doc.Phone = patch.Phone;
        if (patch.Visibility is not null) doc.Visibility = patch.Visibility;
        if (patch.ContactVisibility is not null) doc.ContactVisibility = patch.ContactVisibility;

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
}
