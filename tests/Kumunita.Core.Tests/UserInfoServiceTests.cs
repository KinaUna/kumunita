using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// Plan M1 step 4, implementation step 3 — the three read paths of
/// <see cref="UserInfoService"/> (invariant C4 — strong consistency, live-row reads):
/// <see cref="UserInfoService.GetProfileAsync"/>, <see cref="UserInfoService.GetGroupIdsAsync"/>,
/// <see cref="UserInfoService.GetActiveGrantAsync"/>.
/// <para>
/// Each test hands itself a fresh scratch Postgres DB (<see cref="PostgresFixture.NewDatabaseAsync"/>)
/// so no test clobbers another's catalog. The Marten store is bootstrapped with M0's
/// <see cref="KumunitaFeature"/>, M1's hand-rolled <see cref="AuthorizationFeature"/>, and the
/// M1 domain-document registrations (<see cref="M1DocTypes.Configure"/>), then applied against the
/// fresh DB via <c>ApplyAllConfiguredChangesToDatabaseAsync</c> — the same template the M0/M1 DDL
/// tests use, extended with the document registrations the read paths need.
/// </para>
/// <para>
/// The mutating write paths (<c>UpsertProfileAsync</c>, <c>CreateGroupAsync</c>, add/remove member,
/// <c>GrantDelegationAsync</c>/<c>RevokeDelegationAsync</c>, <c>SeedComponentsAsync</c>,
/// <c>SetComponentModeratorAccessAsync</c>) are now all implemented. Every *write* test drives the
/// service's own methods (the single-<c>SaveChangesAsync</c> authority invariants C3/C4 depend on);
/// the two *pure-read* tests (group membership C4 and active-grant selection) still fixture their
/// input documents directly through a raw session, since those are testing the read half only.
/// </para>
/// </summary>
public class UserInfoServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Test 1 — profile round-trip (plan test 1) ─────────────────────────
    // Upsert a Profile with SubjectId/DisplayName/Verified/Visibility set, read it back
    // through GetProfileAsync; assert every field survives the round-trip, including the
    // Visibility.Mode and the grant list the audit/visibility rules lean on.

    [Fact]
    public async Task GetProfileAsync_RoundTrip_PreservesSubjectIdAndVisibility()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        var subjectId = "u-profile-1";
        var profile = new Profile
        {
            SubjectId = subjectId,
            DisplayName = "Ada Resident",
            Verified = true,
            // ADR 0001-B — author-controlled audience; a non-default (All + named grants) shape
            // so the round-trip proves the nested Visibility document is stored readably.
            Visibility = new Audience(
                AudienceMode.All,
                [new AudienceGrant(GrantKind.User, "u-author")]),
        };

        // Seeded through the service's own UpsertProfileAsync (implementation step 6, now
        // implemented): patch is all-null, so the bootstrap record (profile) supplies every
        // field — including the nested Visibility document — and the assertions below prove
        // every field survives the round-trip.
        await svc.UpsertProfileAsync(profile, new ProfileUpdate(null, null, null, null, null));

        var read = await svc.GetProfileAsync(subjectId);

        Assert.NotNull(read);
        Assert.Equal(subjectId, read!.SubjectId);
        Assert.Equal(profile.DisplayName, read.DisplayName);
        Assert.Equal(profile.Verified, read.Verified);
        Assert.Equal(profile.Visibility.Mode, read.Visibility.Mode);
        var grants = read.Visibility.Grants;
        Assert.Single(grants);
        Assert.Equal(new AudienceGrant(GrantKind.User, "u-author"), grants[0]);

        // Unknown subject -> null (the read path is a plain live-row probe, no default document).
        var missing = await svc.GetProfileAsync("does-not-exist");
        Assert.Null(missing);
    }

    // ── U3 — GetProfilesAsync candidate set (M2 design §2.1, F15) ──────────
    // verifiedOnly=true filters to verified rows only; verifiedOnly=false returns
    // every profile row. Crucially: NEITHER call appends an AccessAudit row (the
    // C-M2·2 "candidate filter is not an access decision" pin at the unit level —
    // the §4.3 filter lives in DirectoryService / here, never in IAuthorizationService,
    // so the audit lane (invariant C3) is not touched by this read).

    [Fact]
    public async Task GetProfilesAsync_VerifiedOnly_Filters()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        // Three residents: two verified, one unverified.
        const string verifiedA = "u-gp-a";
        const string verifiedB = "u-gp-b";
        const string unverifiedC = "u-gp-c";

        foreach (var (subject, verified) in new[]
        {
            (verifiedA, true),
            (verifiedB, true),
            (unverifiedC, false),
        })
        {
            var profile = new Profile
            {
                SubjectId = subject,
                DisplayName = $"Resident {subject}",
                Verified = verified,
                Visibility = new Audience(),
            };
            await svc.UpsertProfileAsync(profile, new ProfileUpdate(null, null, null, null, null));
        }

        // verifiedOnly: true → only the two verified residents.
        var verifiedOnly = await svc.GetProfilesAsync(verifiedOnly: true);
        Assert.Equal(2, verifiedOnly.Count);
        Assert.Contains(verifiedOnly, p => p.SubjectId == verifiedA);
        Assert.Contains(verifiedOnly, p => p.SubjectId == verifiedB);
        Assert.DoesNotContain(verifiedOnly, p => p.SubjectId == unverifiedC);

        // verifiedOnly: false → every profile, including the unverified one.
        var all = await svc.GetProfilesAsync(verifiedOnly: false);
        Assert.Equal(3, all.Count);
        Assert.Contains(all, p => p.SubjectId == unverifiedC);

        // C-M2·2 pin: the candidate filter is *not* an access decision — neither
        // call above appends an AccessAudit row (invariant C3's lane is untouched).
        await using (var session = store.QuerySession())
        {
            var audits = await session.Query<Authorization.AccessAudit>()
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Empty(audits);
        }
    }

    [Fact]
    public async Task CreateGroupAsync_CreatesGroupAndOwnerMembership()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-owner-2";
        var group = await svc.CreateGroupAsync(owner, "Team X", "desc");

        // Owner should be a member immediately
        var groups = await svc.GetGroupIdsAsync(owner);
        Assert.Contains(group.Id, groups);
    }

    [Fact]
    public async Task AddAndRemoveGroupMember_UpdatesMembershipAndWritesAudit()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-owner-3";
        const string member = "u-member";

        var group = await svc.CreateGroupAsync(owner, "Team Y", null);

        // Add member (added by owner)
        await svc.AddGroupMemberAsync(group.Id, member, owner);
        var afterAdd = await svc.GetGroupIdsAsync(member);
        Assert.Contains(group.Id, afterAdd);

        // Check audit row written
        await using (var session = store.QuerySession())
        {
            var audits = await session.Query<Authorization.AccessAudit>()
                .Where(a => a.Action == "group.add-member" && a.TargetId == group.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(audits);
        }

        // Remove member
        await svc.RemoveGroupMemberAsync(group.Id, member, owner);
        var afterRemove = await svc.GetGroupIdsAsync(member);
        Assert.DoesNotContain(group.Id, afterRemove);

        // Check remove audit
        await using (var session = store.QuerySession())
        {
            var audits = await session.Query<Authorization.AccessAudit>()
                .Where(a => a.Action == "group.remove-member" && a.TargetId == group.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(audits);
        }
    }

    // ── Test 2 — live group membership, invariant C4 (plan test 2) ────────
    // Membership changes are live on the *very next* GetGroupIdsAsync call — no projection,
    // no cache, no polling. Add O to A; the next call sees A. Remove O from A; the next
    // call drops A.

    [Fact]
    public async Task GetGroupIdsAsync_LiveMembership_C4_StrongConsistency()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string o = "u-owner";
        var a = new Group { Id = "g-a", Name = "A", OwnerId = o };
        var b = new Group { Id = "g-b", Name = "B", OwnerId = o };

        // A membership row for (A, O). Group is seeded too so the membership's GroupId is
        // a live document (the read path only selects GroupId, but seeding it keeps the
        // fixture honest against the real shape).
        var membership = new GroupMembership { Id = "m-a-o", GroupId = a.Id, UserId = o, AddedBy = o };

        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(a);
            session.Store(b);
            session.Store(membership);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // After the save, the very next call sees O in exactly one group: A.
        var afterAdd = await svc.GetGroupIdsAsync(o);
        Assert.Contains(a.Id, afterAdd);
        Assert.DoesNotContain(b.Id, afterAdd);
        Assert.Single(afterAdd);

        // Delete the membership row (RemoveGroupMemberAsync is step 5; not yet implemented —
        // deletion is the strong-consistency primitive it relies on).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Delete<GroupMembership>(membership.Id);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // The very next call drops A: no cache, no polling, no projection — the live row is
        // the truth (invariant C4).
        var afterRemove = await svc.GetGroupIdsAsync(o);
        Assert.Empty(afterRemove);
    }

    // ── Test 3 — active grant selection (plan test 3) ─────────────────────
    // Three grants for delegate D1: one within [From, To] (active), one expired (past To),
    // one revoked (RevokedBy set). GetActiveGrantAsync returns only the within-window one.
    // A second delegate D2 with only an expired + a revoked grant gets null.

    [Fact]
    public async Task GetActiveGrantAsync_ReturnsOnlyTheActiveGrant()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        var now = DateTimeOffset.UtcNow;
        const string owner = "u-owner";
        const string d1 = "u-d1";
        const string d2 = "u-d2";

        // D1 — three grants for the same delegate; only the within-window one is active.
        var active = new DelegationGrant
        {
            Id = "grant-active",
            OwnerId = owner,
            DelegateId = d1,
            Scope = [AccessAction.Moderate.Id],
            From = now.AddHours(-1),
            To = now.AddHours(1),
        };
        var expired = new DelegationGrant
        {
            Id = "grant-expired",
            OwnerId = owner,
            DelegateId = d1,
            Scope = [AccessAction.Moderate.Id],
            From = now.AddDays(-30),
            To = now.AddDays(-1), // strictly past -> not active for now
        };
        var revoked = new DelegationGrant
        {
            Id = "grant-revoked",
            OwnerId = owner,
            DelegateId = d1,
            Scope = [AccessAction.Moderate.Id],
            From = now.AddDays(-1),
            To = now.AddDays(1), // within the window, but
            RevokedBy = owner,  // revoked -> not active
        };

        // D2 — only an expired grant and a revoked grant; no active one.
        var d2Expired = new DelegationGrant
        {
            Id = "grant-d2-expired",
            OwnerId = owner,
            DelegateId = d2,
            Scope = [AccessAction.Moderate.Id],
            From = now.AddDays(-30),
            To = now.AddDays(-1),
        };
        var d2Revoked = new DelegationGrant
        {
            Id = "grant-d2-revoked",
            OwnerId = owner,
            DelegateId = d2,
            Scope = [AccessAction.Moderate.Id],
            From = now.AddDays(-1),
            To = now.AddDays(1),
            RevokedBy = owner,
        };

        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(active);
            session.Store(expired);
            session.Store(revoked);
            session.Store(d2Expired);
            session.Store(d2Revoked);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Sanity: the seeded documents themselves are consistent with the service's truth
        // (IsActiveAt is the single source — same predicate the service calls).
        Assert.True(active.IsActiveAt(d1, now));
        Assert.False(expired.IsActiveAt(d1, now));
        Assert.False(revoked.IsActiveAt(d1, now));

        // The service picks the within-window grant for D1.
        var d1Active = await svc.GetActiveGrantAsync(d1);
        Assert.NotNull(d1Active);
        Assert.Equal(active.Id, d1Active!.Id);

        // For a delegate whose grants are all expired/revoked, the delegate has no active
        // grant — they act as themselves (null).
        var d2Active = await svc.GetActiveGrantAsync(d2);
        Assert.Null(d2Active);
    }

    // ── shared bootstrap: fresh scratch DB + applied schema + document registrations ──

    /// <summary>
    /// Fresh scratch DB for the calling test, bootstrapped Marten store with M0 + M1 features
    /// and the M1 domain documents, applied against the fresh catalog. The caller owns the
    /// returned store's lifetime (typically a <c>using</c> block) — the scratch DB itself is
    /// container-lifetime, so the caller does not dispose it.
    /// </summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    // ── Test 7 — delegation grant (plan test 7) ──────────────────────────
    // Granting creates a live grant on the very next GetActiveGrantAsync call
    // (invariant C4) and records the admin-action Audit row (Action
    // "delegation.grant", ActorId = the grantor, TargetKind "delegation_grant",
    // TargetId the new grant) in the same commit (invariant C3).

    [Fact]
    public async Task GrantDelegationAsync_CreatesLiveGrantAndAuditRow()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-owner-grant";
        const string delegatee = "u-delegate-grant";
        var now = DateTimeOffset.UtcNow;
        var scope = new[] { AccessAction.Moderate.Id };

        var grant = await svc.GrantDelegationAsync(owner, delegatee, scope, now.AddHours(-1), now.AddHours(1));

        // Shape survives the round-trip (id is freshly minted by the service).
        Assert.Equal(owner, grant.OwnerId);
        Assert.Equal(delegatee, grant.DelegateId);
        Assert.Equal(scope, grant.Scope);
        Assert.False(grant.To is null);

        // Strong consistency (invariant C4) — the very next read sees it live
        // as the delegate's active grant (invariant C2's "within window, not
        // revoked" predicate, via the shared IsActiveAt truth).
        var active = await svc.GetActiveGrantAsync(delegatee);
        Assert.NotNull(active);
        Assert.Equal(grant.Id, active!.Id);

        // The granting account itself is unaffected — they act as themselves.
        Assert.Null(await svc.GetActiveGrantAsync(owner));

        // The audit row (always-on, invariant C3) is in the same commit as the
        // grant: one row, targeting the new grant, via the grantor's standing.
        await using (var session = store.QuerySession())
        {
            var audit = await session.Query<AccessAudit>()
                .Where(a => a.Action == "delegation.grant" && a.TargetId == grant.Id)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(audit);
            Assert.Equal(owner, audit!.ActorId);
            Assert.Equal(grant.Id, audit.TargetId);
            Assert.Equal(AccessVia.Owner, audit.Via);
            Assert.Equal(AccessOutcome.Allow, audit.Outcome);
        }
    }

    // ── Test 8 — delegation revoke (plan test 8) ───────────────────────
    // Revoking closes the grant (RevokedBy set, row kept) and the very next
    // GetActiveGrantAsync call no longer returns it (invariant C4); the
    // "delegation.revoke" Audit row is recorded in the same commit (C3) with
    // Via per the derivation rule (Owner when the owner revokes, else Admin).

    [Fact]
    public async Task RevokeDelegationAsync_ClosesGrantAndWritesAuditRow()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-owner-revoke";
        const string delegatee = "u-delegate-revoke";
        var now = DateTimeOffset.UtcNow;
        var scope = new[] { AccessAction.Moderate.Id };

        var grant = await svc.GrantDelegationAsync(owner, delegatee, scope, now.AddHours(-1), now.AddHours(1));

        // Sanity: the grant is active before the revoke (invariant C2).
        Assert.NotNull(await svc.GetActiveGrantAsync(delegatee));

        // The owner revokes — Via must be Owner (the derivation rule).
        await svc.RevokeDelegationAsync(grant.Id, owner);

        // Strong consistency (invariant C4) — the loss is live on the very next
        // call: the delegate no longer has an active grant.
        Assert.Null(await svc.GetActiveGrantAsync(delegatee));

        // The row is kept (history) and closed.
        await using (var session = store.QuerySession())
        {
            var stored = await session.LoadAsync<DelegationGrant>(grant.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(stored);
            Assert.Equal(owner, stored!.RevokedBy);

            var audit = await session.Query<AccessAudit>()
                .Where(a => a.Action == "delegation.revoke" && a.TargetId == grant.Id)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(audit);
            Assert.Equal(owner, audit!.ActorId);
            Assert.Equal(AccessVia.Owner, audit.Via);
            Assert.Equal(owner, audit.EffectivePrincipalId);
            Assert.Equal(AccessOutcome.Allow, audit.Outcome);
        }
    }

    // ── Test 9 — components seed (plan test 9) ───────────────────────────────
    // The four default components (safety/maintenance/social/governance — id IS the stable
    // key, per the stub/interface doc) all exist after the first SeedComponentsAsync call,
    // each with ModeratorAccess = false (invariant C5's OFF-by-default). An idempotent
    // re-run leaves the set at exactly four and does NOT touch an existing flag — only
    // SetComponentModeratorAccessAsync flips it, so a deliberate ON survives a re-seed.

    [Fact]
    public async Task SeedComponentsAsync_CreatesFour_AllModeratorAccessFalse_IdempotentReRun()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        var seeded = await svc.SeedComponentsAsync();

        var ids = seeded.Select(c => c.Id).ToHashSet();
        Assert.Equal(new[] { "safety", "maintenance", "social", "governance" }.ToHashSet(), ids);
        // C5: every newly created component starts OFF (the default is the pinned row).
        Assert.All(seeded, c => Assert.False(c.ModeratorAccess));

        // An existing row's deliberate ON must survive a re-seed (C5's "only the flag path
        // flips it" — re-seeding never resets it).
        const string actor = "admin-seed";
        await svc.SetComponentModeratorAccessAsync("safety", true, actor);
        await using (var session = store.QuerySession())
        {
            Assert.True((await session.LoadAsync<Component>("safety", TestContext.Current.CancellationToken))!.ModeratorAccess);
        }

        var reseeded = await svc.SeedComponentsAsync();

        Assert.Equal(4, reseeded.Count);
        Assert.Contains(reseeded, c => c.Id == "safety" && c.ModeratorAccess);
        Assert.All(reseeded.Where(c => c.Id != "safety"), c => Assert.False(c.ModeratorAccess));
    }

    // ── Test 10 — component moderator-access flip (plan test 10) ─────────────
    // Flipping the flag on, then off, each appends its own "moderator-access" audit row
    // (Via = Admin unambiguously — the interface doc pins this surface to a GlobalAdmin —
    // Action "moderator-access", TargetKind "component", TargetId the component id,
    // Outcome Allow) in the SAME commit as the flag change (C3 — flag and audit row are
    // written together, so neither can commit without the other).

    [Fact]
    public async Task SetComponentModeratorAccessAsync_FlipsFlag_AuditViaAdminTargetComponent()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        await svc.SeedComponentsAsync();

        const string actor = "u-admin";
        const string component = "social";

        await svc.SetComponentModeratorAccessAsync(component, true, actor);
        await using (var session = store.QuerySession())
        {
            Assert.True((await session.LoadAsync<Component>(component, TestContext.Current.CancellationToken))!.ModeratorAccess);

            var on = await session.Query<AccessAudit>()
                .Where(a => a.Action == "moderator-access" && a.TargetId == component)
                .FirstOrDefaultAsync(a => a.Via == AccessVia.Admin && a.ActorId == actor, TestContext.Current.CancellationToken);
            Assert.NotNull(on);
            Assert.Equal(component, on!.TargetId);
            Assert.Equal("component", on.TargetKind);
            Assert.Equal(AccessVia.Admin, on.Via);
            Assert.Equal(actor, on.EffectivePrincipalId);
            Assert.Equal(AccessOutcome.Allow, on.Outcome);
        }

        await svc.SetComponentModeratorAccessAsync(component, false, actor);
        await using (var session = store.QuerySession())
        {
            Assert.False((await session.LoadAsync<Component>(component, TestContext.Current.CancellationToken))!.ModeratorAccess);
            Assert.Equal(2, await session.Query<AccessAudit>()
                .Where(a => a.Action == "moderator-access" && a.TargetId == component)
                .CountAsync(TestContext.Current.CancellationToken));
        }
    }

    // ── Test 11 — profile upsert (plan test 11) ────────────────────────────────
    // Create-when-absent: a fresh SubjectId produces a row built from the `profile`
    // argument's base fields (DisplayName/Email/Verified/Visibility). A subsequent upsert
    // with a patch that sets DisplayName and leaves Email null takes the patch's value on
    // DisplayName and leaves the stored Email untouched (the "non-null patch field takes
    // priority; null patch field leaves the current value" rule from the step-4 doc's risk
    // note).

    [Fact]
    public async Task UpsertProfileAsync_CreatesWhenAbsent_LeavesNullPatchFieldsUntouched()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        var subject = "u-upsert";
        var bootstrap = new Profile
        {
            SubjectId = subject,
            DisplayName = "First Name",
            Email = "first@example.com",
            Verified = true,
            Visibility = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, subject)]),
        };
        await svc.UpsertProfileAsync(bootstrap, new ProfileUpdate(null, null, null, null, null));

        var created = await svc.GetProfileAsync(subject);
        Assert.NotNull(created);
        Assert.Equal(bootstrap.DisplayName, created!.DisplayName);
        Assert.Equal(bootstrap.Email, created!.Email);
        Assert.True(created.Verified);

        // A patch that sets DisplayName and leaves Email null updates only DisplayName.
        await svc.UpsertProfileAsync(
            bootstrap, // identity/context; patch's non-null fields take priority
            new ProfileUpdate("Second Name", null, null, null, null));

        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.Equal("Second Name", read!.DisplayName);
        Assert.Equal(bootstrap.Email, read!.Email);
    }
}
