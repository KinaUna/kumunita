using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M2, plan U6 — the five <see cref="DirectoryService"/> seam tests (the M2 invariant-
/// anchored seam list U11/U12 record against the acceptance gate, design doc §2.6).
/// Each test name is pinned by plan U6 and the §2.5 FACES/invariant anchor it cites
/// (C-M2·1 contact-gating ordering; C-M2·2 candidate filter ≠ access decision, §4.3;
/// ADR 0006-C2 delegation action-scoped; C6 no-drift property; C4 live membership,
/// C-M2·3 group SoD). The tests compose U5's <see cref="DirectoryService"/> over the
/// two frozen seams (<c>IUserInfoService</c> + <c>IAuthorizationService</c>) using
/// U4's <see cref="ProfileToAuditableResource"/> — no new Core surface, matching the
/// drift-guard §2.7. Deliberately a *second* file: U5's own note, appended to
/// <c>DirectoryServiceTests.cs</c>, pins that file as U5's closed self-check set and
/// instructs this unit to "append it to the file I shipped" for that specific U5 test;
/// the plan U6 file's "one file, five tests" deliverable is kept as its own file so
/// U5's shipped assertion block (and U5's one-test class) stays exactly as its test
/// expects it, and U6's five tests are a clean, self-contained addition to the same
/// assembly — the same <c>PostgresFixture</c>, same <c>BootStoreAsync</c> shape,
/// same service-composition pattern U5 established.
/// </summary>
public class DirectoryServiceTests_U6(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Test 1 — ContactVisibility_FourShape_TrightTable (§2.4, C-M2·1) ──────
    //
    // The design doc §2.4's four-shape truth table for the *contact-block* gate —
    // the single audience decision on the detail surface (C-M2·1, now a one-decision
    // rule: the profile's basic info renders always; only the contact block is
    // audience-gated). For each shape we assert the returned `ShowContactBlock` AND
    // the exact audit-row count for that profile:
    //   • row 1 (`null`) — "null ⇒ not opted in" short-circuit: basic info renders
    //     (Profile non-null), contact NOT evaluated, so ZERO audit rows (a null
    //     contact audience is not a Deny and not an evaluation).
    //   • row 2 (`Any` + empty grants) — C1 empty-audience guard: one decision
    //     (a C6-compliant shared-matching-pass call) that Denies; exactly ONE audit
    //     row, `ShowContactBlock == false`.
    //   • row 3 (`Any` + grant, viewer in-grant) — evaluates through
    //     <c>MatchGroups</c>; exactly one row, an Allow. `ShowContactBlock == true`.
    //   • row 4 (`All` + empty grants) — C1 `All` + empty denies (the vacuous-truth
    //     guard this invariant exists for): exactly one row, a Deny.
    //     `ShowContactBlock == false`.
    //
    // Each variant gets a *distinct* target profile (a fresh scratch DB per test
    // method does not isolate within-method steps) so the per-target audit-row
    // counts below are unambiguous.

    [Fact]
    public async Task ContactVisibility_FourShape_TrightTable()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new DirectoryService(userInfo, authz);

        const string viewer = "u-u6-t1-viewer";

        // Plant four distinct target profiles, one per §2.4 row. Each has a
        // `Visibility` allowing the viewer (the gating precondition); the only
        // variation is the `ContactVisibility` shape.
        var visibility = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, viewer)]);

        var rowNull = new Profile
        {
            SubjectId = "u6-t1-null",
            DisplayName = "Row null",
            Verified = true,
            Visibility = visibility,
            ContactVisibility = null, // row 1: not evaluated (short-circuit, §9)
        };

        var rowAnyEmpty = new Profile
        {
            SubjectId = "u6-t1-any-empty",
            DisplayName = "Row Any + empty",
            Verified = true,
            Visibility = visibility,
            ContactVisibility = new Audience(AudienceMode.Any, Array.Empty<AudienceGrant>()), // row 2: C1
        };

        var rowAnyGrant = new Profile
        {
            SubjectId = "u6-t1-any-grant",
            DisplayName = "Row Any + grant",
            Verified = true,
            Visibility = visibility,
            ContactVisibility = new Audience(AudienceMode.Any,
                [new AudienceGrant(GrantKind.User, viewer)]), // row 3: evaluates through MatchGroups
        };

        var rowAllEmpty = new Profile
        {
            SubjectId = "u6-t1-all-empty",
            DisplayName = "Row All + empty",
            Verified = true,
            Visibility = visibility,
            ContactVisibility = new Audience(AudienceMode.All, Array.Empty<AudienceGrant>()), // row 4: C1
        };

        await userInfo.UpsertProfileAsync(rowNull, new ProfileUpdate(null, null, null, null, null));
        await userInfo.UpsertProfileAsync(rowAnyEmpty, new ProfileUpdate(null, null, null, null, null));
        await userInfo.UpsertProfileAsync(rowAnyGrant, new ProfileUpdate(null, null, null, null, null));
        await userInfo.UpsertProfileAsync(rowAllEmpty, new ProfileUpdate(null, null, null, null, null));

        // Row 1 — `null` ContactVisibility: the §2.4 "null ⇒ not opted in" short-circuit —
        // basic info still renders (Profile is non-null), contact NOT evaluated, so NO
        // audit row (not a Deny, not an evaluation — an early return before any CanAsync).
        var d1 = await svc.DetailAsync(viewer, rowNull.SubjectId);
        Assert.NotNull(d1!.Profile);
        Assert.False(d1.ShowContactBlock);
        Assert.Equal(0, await RowCount(store, rowNull.SubjectId));

        // Row 2 — `Any` + empty: evaluates (a single decision — the §2.4 pin),
        // Deny (C1 empty-audience guard), no contact rendered. Exactly one audit row.
        var d2 = await svc.DetailAsync(viewer, rowAnyEmpty.SubjectId);
        Assert.NotNull(d2!.Profile);
        Assert.False(d2.ShowContactBlock);
        Assert.Equal(1, await RowCount(store, rowAnyEmpty.SubjectId));
        Assert.Single(await OutcomeRowCount(store, rowAnyEmpty.SubjectId, AccessOutcome.Deny));

        // Row 3 — `Any` + grant the viewer is in: evaluates through MatchGroups,
        // Allow, contact rendered. Exactly one audit row.
        var d3 = await svc.DetailAsync(viewer, rowAnyGrant.SubjectId);
        Assert.NotNull(d3!.Profile);
        Assert.True(d3.ShowContactBlock);
        Assert.Equal(1, await RowCount(store, rowAnyGrant.SubjectId));
        Assert.Single(await OutcomeRowCount(store, rowAnyGrant.SubjectId, AccessOutcome.Allow));

        // Row 4 — `All` + empty: evaluates, Deny (the vacuous-truth guard, C1).
        var d4 = await svc.DetailAsync(viewer, rowAllEmpty.SubjectId);
        Assert.NotNull(d4!.Profile);
        Assert.False(d4.ShowContactBlock);
        Assert.Equal(1, await RowCount(store, rowAllEmpty.SubjectId));
        Assert.Single(await OutcomeRowCount(store, rowAllEmpty.SubjectId, AccessOutcome.Deny));
    }

    // ── Test 2 — UnverifiedViewer_Lists_All_NonBlock_Residents (show-everyone) ─
    //
    // The directory's product rule (invitation-only, resident-limited): the list is
    // NOT narrowed by verification or by audience. U6's pin — the plan's intent,
    // restated — is that an *unverified* resident's `ListAsync` result includes
    // OTHER residents (a verified one whose audience granted them is in `Visible`,
    // their own row is in `Visible`, the blocked one is not), and that the list ran
    // NO `IAuthorizationService` decision at all: the list renders zero
    // `AccessAudit` rows (nothing to name any resident as actor/principal/target).

    [Fact]
    public async Task UnverifiedViewer_Lists_All_NonBlock_Residents()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new DirectoryService(userInfo, authz);

        const string verifiedOther = "u-u6-t2-verified-other";
        const string unverifiedViewer = "u-u6-t2-unverified-viewer";
        const string blockedResident = "u-u6-t2-blocked-resident";

        // A verified resident whose *own* audience would have allowed the viewer —
        // so the test proves the result is driven by the show-everyone rule, *not*
        // by audience evaluation: under the old two-gate design, audience evaluation
        // would have excluded this one from the unverified viewer's list.
        await userInfo.UpsertProfileAsync(
            new Profile
            {
                SubjectId = verifiedOther,
                DisplayName = "Verified Other",
                Verified = true,
                Visibility = new Audience(AudienceMode.Any,
                    [new AudienceGrant(GrantKind.User, unverifiedViewer)]),
            },
            new ProfileUpdate(null, null, null, null, null));

        await userInfo.UpsertProfileAsync(
            new Profile
            {
                SubjectId = unverifiedViewer,
                DisplayName = "Unverified Viewer",
                Verified = false,
                Visibility = new Audience(AudienceMode.Any,
                    [new AudienceGrant(GrantKind.User, unverifiedViewer)]),
            },
            new ProfileUpdate(null, null, null, null, null));

        // A suspended resident: excluded by Profile.Blocked, the only account-level
        // exclusion that survives on the list. (BlockAsync is the real admin lane —
        // it needs UserManager; we set the flag via the same single-Store the
        // admin lane's core line does.)
        await using (var blockedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            blockedSession.Store(new Profile
            {
                SubjectId = blockedResident,
                DisplayName = "Blocked Resident",
                Verified = true,
                Blocked = true,
            });
            await blockedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await svc.ListAsync(unverifiedViewer);

        var ids = result.Visible.Select(p => p.SubjectId).ToHashSet();
        Assert.Contains(verifiedOther, ids);
        Assert.Contains(unverifiedViewer, ids);
        Assert.DoesNotContain(blockedResident, ids);
        Assert.Equal(2, result.Visible.Count);

        // The list rendered zero AccessAudit rows (a pure catalog read — nothing
        // naming any resident as actor, principal, or target).
        await using var auditSession = store.QuerySession();
        var allRows = await auditSession.Query<AccessAudit>()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(allRows);
    }

    // ── Test 3 — DelegationOnProfile_OwnerBranch (F9, ADR 0006-C2) ─────────
    //
    // The *same* C2 invariant M1's `Post`-shaped `AuthorizationServiceTests`
    // already covers, now pinned for `Profile` (via U4's <see cref="ProfileToAuditableResource"/>),
    // the plan's exact wording — "a grant's `Owner` branch exercised through
    // `CanAsync(Read, profile)` (not the `ContactVisibility` branch — same
    // invariant on a different resource shape)":
    //   (a) in-scope (`Read`), the delegate borrows the owner's standing and the
    //       decision lands on the **Owner branch** (`Via == Owner`, not
    //       `Audience` — the owner branch is branch 1 of the §4.4 algorithm and
    //       fires *before* `MatchGroups`), with `EffectivePrincipalId` = the owner;
    //   (b) out-of-scope (`Moderate`), the same owner standing does *not* transfer
    //       — the decision is a Deny recording the acting identity (`Via ==
    //       Delegation`, `EffectivePrincipalId` = the delegate), the C2 "out-of-
    //       scope is a Deny even though the effective principal would have been
    //       the owner" pin, on the *Profile* resource shape.

    [Fact]
    public async Task DelegationOnProfile_OwnerBranch()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);

        const string owner = "u-u6-t3-owner";
        const string delegatee = "u-u6-t3-delegatee";
        var now = DateTimeOffset.UtcNow;

        // Grant covers `Read` only. Out-of-scope below uses `Moderate`.
        await userInfo.GrantDelegationAsync(owner, delegatee,
            scope: [AccessAction.Read.Id],
            from: now.AddHours(-1), to: now.AddHours(1));

        // Owner's profile, audience restricted to the owner themselves (the owner
        // branch's *only* audience — the delegate is in neither it nor the owner
        // identity, so `MatchGroups` would not allow them either: the Allow that
        // follows in (a) can only come from the owner branch, which is what we are
        // proving the Owner-branch-on-`Profile` invariant to be).
        await userInfo.UpsertProfileAsync(
            new Profile
            {
                SubjectId = owner,
                DisplayName = "Owner",
                Verified = true,
                Visibility = new Audience(AudienceMode.Any,
                    [new AudienceGrant(GrantKind.User, owner)]),
            },
            new ProfileUpdate(null, null, null, null, null));

        var profileResource = new ProfileToAuditableResource(
            (await userInfo.GetProfileAsync(owner))!);

        // (a) In-scope: delegate borrows the owner's standing → Owner branch fires
        // (before MatchGroups). Via = Delegation (the mechanism of access);
        // EffectivePrincipalId = the owner (proving the owner branch fired and the
        // delegate is standing in the owner's shoes, §4.4 branch 1). The audit
        // row's own fields carry the same values (C3).
        var inScope = await authz.CanAsync(delegatee, AccessAction.Read, profileResource);
        Assert.True(inScope.Allowed);
        Assert.Equal(AccessVia.Delegation, inScope.Via);
        Assert.Equal(owner, inScope.EffectivePrincipalId);

        await using (var inScopeSession = store.QuerySession())
        {
            var inScopeRow = await inScopeSession.Query<AccessAudit>()
                .Where(a => a.Action == AccessAction.Read.Id && a.TargetId == owner)
                .Where(a => a.ActorId == delegatee && a.Outcome == AccessOutcome.Allow)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(inScopeRow);
            Assert.Equal(AccessVia.Delegation, inScopeRow!.Via);
            Assert.Equal(owner, inScopeRow.EffectivePrincipalId);
            Assert.Equal("directory", inScopeRow.TargetKind);
        }

        // (b) Out-of-scope (`Moderate`): the owner's standing is NOT borrowed for
        // this action; the delegate's own standing (self, not the owner, not in
        // the owner-only audience) denies. The Deny records the acting identity:
        // Via = Delegation, EffectivePrincipalId = the delegate themselves.
        var outOfScope = await authz.CanAsync(delegatee, AccessAction.Moderate, profileResource);
        Assert.False(outOfScope.Allowed);
        Assert.Equal(AccessVia.Delegation, outOfScope.Via);
        Assert.Equal(delegatee, outOfScope.EffectivePrincipalId);

        await using (var outOfScopeSession = store.QuerySession())
        {
            var outOfScopeRow = await outOfScopeSession.Query<AccessAudit>()
                .Where(a => a.Action == AccessAction.Moderate.Id && a.TargetId == owner)
                .Where(a => a.ActorId == delegatee)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(outOfScopeRow);
            Assert.Equal(AccessVia.Delegation, outOfScopeRow!.Via);
            Assert.Equal(AccessOutcome.Deny, outOfScopeRow.Outcome);
            Assert.Equal(delegatee, outOfScopeRow.EffectivePrincipalId);
        }
    }

    // ── Test 4 — CanAsync_Equals_CanSeeAsync_SingleRow_Profile (C6, F11) ───
    //
    // U5's `ListAsync_Hides_Unverified` already exercises both overloads over a
    // single-candidate set (aggregate + per-item shape). U6's addition is the
    // *decision-level equivalence* the plan names — "pick a profile where
    // `CanSeeAsync` allows, assert `CanAsync` on the same profile with the same
    // arguments also allows, and vice-versa for `Deny`" — on a `Profile` (via U4's
    // adapter), for the Allow case AND the Deny case, and checks that the audit
    // row(s) on *both* overloads agree with the decisions (U5's note: assert on
    // `Via`/`Outcome`/`TargetKind`/`TargetId` and the row's existence — not on
    // which `Can*` overload was called internally):
    //   Allow: `CanAsync`'s one row and `CanSeeAsync`'s aggregate + per-item two
    //            rows are all `Allow`/`Owner`, and `CanSeeAsync`'s visible row's
    //            `Via` matches `CanAsync`'s `Via`.
    //   Deny:  `CanAsync`'s one row and `CanSeeAsync`'s aggregate + per-item two
    //            rows are all `Deny`/`Audience` (the actor's own standing, no
    //            owner/audience match, no delegation grant planted), and
    //            `CanSeeAsync`'s `Visible` set is empty (i.e. "denied").

    [Fact]
    public async Task CanAsync_Equals_CanSeeAsync_SingleRow_Profile()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);

        const string owner = "u-u6-t4-owner";
        const string other = "u-u6-t4-other";

        await userInfo.UpsertProfileAsync(
            new Profile
            {
                SubjectId = owner,
                DisplayName = "Owner",
                Verified = true,
                Visibility = new Audience(AudienceMode.Any,
                    [new AudienceGrant(GrantKind.User, owner)]),
            },
            new ProfileUpdate(null, null, null, null, null));

        var resource = new ProfileToAuditableResource((await userInfo.GetProfileAsync(owner))!);

        // ── Allow: owner reads their own profile (Owner branch, §4.4 branch 1) ─
        var singleAllow = await authz.CanAsync(owner, AccessAction.Read, resource);
        Assert.True(singleAllow.Allowed);
        Assert.Equal(AccessVia.Owner, singleAllow.Via);
        Assert.Equal(owner, singleAllow.EffectivePrincipalId);

        var bulkAllow = await authz.CanSeeAsync(owner, AccessAction.Read, [resource]);
        Assert.Single(bulkAllow.Visible);
        Assert.Equal(owner, bulkAllow.Visible[0].Id);
        Assert.Equal(0, bulkAllow.HiddenCount);

        // C6's decision-level agreement: same `Allowed`, same `Via`, same
        // effective principal, for the two overloads over this one candidate.
        Assert.Equal(singleAllow.Allowed, bulkAllow.Visible.Count > 0);
        Assert.Equal(owner, bulkAllow.Visible[0].Id);
        Assert.Equal(singleAllow.Via, bulkAllow.Visible[0].Via);

        // C3's audit-row shape (3 rows total: CanAsync's 1 + CanSeeAsync's 1
        // aggregate + 1 per-item) agrees with the Allow decision.
        await using (var allowSession = store.QuerySession())
        {
            var allowRows = await allowSession.Query<AccessAudit>()
                .Where(a => a.Action == AccessAction.Read.Id && a.TargetKind == "directory"
                            && a.ActorId == owner)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(3, allowRows.Count);
            Assert.All(allowRows, a =>
            {
                Assert.Equal(AccessOutcome.Allow, a.Outcome);
                Assert.Equal(AccessVia.Owner, a.Via);
                Assert.Equal(owner, a.EffectivePrincipalId);
            });
        }

        // ── Deny: a different account (not owner, not in the owner-only audience) ─
        var singleDeny = await authz.CanAsync(other, AccessAction.Read, resource);
        Assert.False(singleDeny.Allowed);
        Assert.Equal(AccessVia.Audience, singleDeny.Via); // the default `Via` for a non-delegated, non-owner Deny (§4.4 branch 6)
        Assert.Equal(other, singleDeny.EffectivePrincipalId);

        var bulkDeny = await authz.CanSeeAsync(other, AccessAction.Read, [resource]);
        Assert.Empty(bulkDeny.Visible);
        Assert.Equal(1, bulkDeny.HiddenCount);

        // C6's decision-level agreement, Deny side: both overloads record the same
        // denial (`Visible` empty ⇔ `Allowed` false, `Via` and effective
        // principal pinned to the actor's own standing).
        Assert.False(singleDeny.Allowed);
        Assert.Equal(AccessVia.Audience, singleDeny.Via);
        Assert.Equal(other, singleDeny.EffectivePrincipalId);

        // C3's audit-row shape (3 rows total: CanAsync's 1 + CanSeeAsync's 1
        // aggregate + 1 per-item) agrees with the Deny decision.
        await using (var denySession = store.QuerySession())
        {
            var denyRows = await denySession.Query<AccessAudit>()
                .Where(a => a.Action == AccessAction.Read.Id && a.TargetKind == "directory"
                            && a.ActorId == other)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(3, denyRows.Count);
            Assert.All(denyRows, a =>
            {
                Assert.Equal(AccessOutcome.Deny, a.Outcome);
                Assert.Equal(AccessVia.Audience, a.Via);
                Assert.Equal(other, a.EffectivePrincipalId);
            });
        }
    }

    // ── Test 5 — GroupAddRemoveMember_ReflectedOnNext_Call_Profile (F2, F7, C4,
    //    C-M2·3) ─────────────────────────────────────────────────────────────
    //
    // The plan's intent — "C4 for *profiles' own audiences*: a member add in the
    // same commit is reflected on the *directory decision* that reads the group" —
    // exercised on the *directory* surface, now over a `Profile` whose
    // <b>ContactVisibility</b> is a `Group`-grant (the "reuse unit" F1 named),
    // because the contact block is the audience the directory surfaces gate on
    // (the show-everyone rule removes the list-level audience gate). The
    // `AddGroupMemberAsync` / `RemoveGroupMemberAsync` audit rows keep their own
    // shape (C3, and C-M2·3's SoD pin — the `ActorId` is the *owner*, the only
    // standing that reaches those methods, per the interface's own docs):
    //   (a) pre-add: the member's directory *list* already includes the owner
    //       (show-everyone), but the *detail*'s contact block is denied (the
    //       group-scoped ContactVisibility is not in the member's groups).
    //   (b) `AddGroupMemberAsync`'s own `AccessAudit` row: `Allow`/`Via = Owner`/
    //       `TargetKind = "group"`/`ActorId = owner`.
    //   (c) post-add: the *very next* `DetailAsync` shows the contact block (C4 /
    //       F2 live-on-next-call — the interface doc's own "live on the next
    //       GetGroupIdsAsync call" wording, here as the next *contact* decision);
    //       the owner's basic info was already visible the whole time.
    //   (d) `RemoveGroupMemberAsync`'s own `AccessAudit` row, same shape pin; then
    //       the very next `DetailAsync` is back to contact-hidden — "the loss of
    //       access is live on the next ... call", per the interface doc.

    [Fact]
    public async Task GroupAddRemoveMember_ReflectedOnNext_Call_Profile()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new DirectoryService(userInfo, authz);

        const string owner = "u-u6-t5-owner";
        const string member = "u-u6-t5-member";

        // Owner's profile: group-scoped *contact* block (a `Group` grant — the
        // "reuse unit" F1 named) — only group members can see the contact block.
        // Basic info (name + verified) renders for everyone, per the show-everyone
        // rule, regardless of this audience.
        var group = await userInfo.CreateGroupAsync(owner, "U6 T5 group", null);
        await userInfo.UpsertProfileAsync(
            new Profile
            {
                SubjectId = owner,
                DisplayName = "Owner",
                Verified = true,
                ContactVisibility = new Audience(AudienceMode.Any,
                    [new AudienceGrant(GrantKind.Group, group.Id)]),
            },
            new ProfileUpdate(null, null, null, null, null));

        // The member's own profile (a minimal row so the directory can list them;
        // the show-everyone rule means their own presence isn't a baseline — it's
        // just one of N non-blocked residents).
        await userInfo.UpsertProfileAsync(
            new Profile
            {
                SubjectId = member,
                DisplayName = "Member",
                Verified = true,
            },
            new ProfileUpdate(null, null, null, null, null));

        // (a) Pre-add: the member's list already includes the owner (show-everyone;
        // no hidden-count concept), but the detail's contact block is denied (the
        // group-scoped ContactVisibility is not in the member's groups).
        var beforeList = await svc.ListAsync(member);
        Assert.Contains(beforeList.Visible, p => p.SubjectId == owner);
        Assert.Contains(beforeList.Visible, p => p.SubjectId == member);
        var beforeDetail = await svc.DetailAsync(member, owner);
        Assert.NotNull(beforeDetail.Profile);
        Assert.False(beforeDetail.ShowContactBlock);

        // (b) `AddGroupMemberAsync`'s own `AccessAudit` row (C3; C-M2·3 SoD — the
        // `ActorId` is the owner, the only standing that reaches it, per the
        // interface's own docs; `Via = Owner`, since `addedBy == group.OwnerId`).
        await userInfo.AddGroupMemberAsync(group.Id, member, addedBy: owner);

        await using (var addSession = store.QuerySession())
        {
            var addAudit = await addSession.Query<AccessAudit>()
                .Where(a => a.Action == "group.add-member" && a.TargetId == group.Id)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(addAudit);
            Assert.Equal(AccessOutcome.Allow, addAudit!.Outcome);
            Assert.Equal(AccessVia.Owner, addAudit.Via);
            Assert.Equal("group", addAudit.TargetKind);
            Assert.Equal(owner, addAudit.ActorId);
        }

        // (c) Post-add: the *very next* `DetailAsync` shows the contact block
        // (C4/F2 live-on-next-call); the owner's basic info was visible the whole
        // time (unchanged), and the list still includes both residents.
        var afterAddList = await svc.ListAsync(member);
        Assert.Contains(afterAddList.Visible, p => p.SubjectId == owner);
        Assert.Contains(afterAddList.Visible, p => p.SubjectId == member);
        var afterAddDetail = await svc.DetailAsync(member, owner);
        Assert.NotNull(afterAddDetail.Profile);
        Assert.True(afterAddDetail.ShowContactBlock);

        // (d) `RemoveGroupMemberAsync`'s own `AccessAudit` row, same shape pin.
        await userInfo.RemoveGroupMemberAsync(group.Id, member, removedBy: owner);

        await using (var removeSession = store.QuerySession())
        {
            var removeAudit = await removeSession.Query<AccessAudit>()
                .Where(a => a.Action == "group.remove-member" && a.TargetId == group.Id)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(removeAudit);
            Assert.Equal(AccessOutcome.Allow, removeAudit!.Outcome);
            Assert.Equal(AccessVia.Owner, removeAudit.Via);
            Assert.Equal("group", removeAudit.TargetKind);
            Assert.Equal(owner, removeAudit.ActorId);
        }

        // (e) The loss of the *contact* access is live on the very next
        // `DetailAsync` (C4/F2, the `RemoveGroupMemberAsync` interface doc's own
        // wording). The list is unchanged (show-everyone); only the contact block
        // flips back to hidden.
        var afterRemoveList = await svc.ListAsync(member);
        Assert.Contains(afterRemoveList.Visible, p => p.SubjectId == member);
        Assert.Contains(afterRemoveList.Visible, p => p.SubjectId == owner);
        var afterRemoveDetail = await svc.DetailAsync(member, owner);
        Assert.NotNull(afterRemoveDetail.Profile);
        Assert.False(afterRemoveDetail.ShowContactBlock);
    }

    // ── Test — SelfView_Always_Shows_Full_ContactBlock (owner exemption) ─────
    //
    // A resident's <b>own</b> profile is never gated by the contact audience: the
    // saved `ContactVisibility` is an author control on what *others* see, not a way
    // to hide one's own data from oneself. Two shapes pin both sides of the rule:
    //   • `null` ContactVisibility (opted out): an outside viewer still gets the
    //     §2.4 "null ⇒ not opted in" short-circuit (contact hidden, no audit row);
    //     the owner sees the full contact block, and the self-view commits no audit
    //     row (a short-circuit, not an evaluation — no `CanAsync`, no row).
    //   • Audience that excludes the owner (e.g. a grant to another resident): the
    //     outside viewer Denies (one audit row, unchanged §2.4 row 2); the owner
    //     still sees the contact block, and their self-view adds no further audit
    //     row (the count stays at exactly the outside viewer's one Deny).

    [Fact]
    public async Task SelfView_Always_Shows_Full_ContactBlock()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new DirectoryService(userInfo, authz);

        const string otherViewer = "u6-self-other-viewer";

        var optedOut = new Profile
        {
            SubjectId = "u6-self-opted-out",
            DisplayName = "Opted out owner",
            Verified = true,
            Email = "optedout@example.com",
            Phone = "+356 9900 0004",
            ContactVisibility = null, // author opted out: hidden from *others*
        };

        var ownerExcluding = new Profile
        {
            SubjectId = "u6-self-owner-excluding",
            DisplayName = "Owner excluding audience",
            Verified = true,
            Email = "ownerexcl@example.com",
            Phone = "+356 9900 0005",
            // A grant to a third resident — neither the owner nor the outside
            // viewer is in the audience, so the outside viewer Denies, but the
            // owner still sees their own data (self-view is not gated).
            ContactVisibility = new Audience(AudienceMode.Any,
                [new AudienceGrant(GrantKind.User, "u6-self-other-resident")]),
        };

        await userInfo.UpsertProfileAsync(optedOut, new ProfileUpdate(null, null, null, null, null));
        await userInfo.UpsertProfileAsync(ownerExcluding, new ProfileUpdate(null, null, null, null, null));

        // Shape 1 — `null` audience: the outside viewer keeps the §2.4 row-1
        // short-circuit (hidden, no audit row); the owner sees the full block with
        // no new audit row (a short-circuit, not a decision).
        var outside = await svc.DetailAsync(otherViewer, optedOut.SubjectId);
        Assert.NotNull(outside.Profile);
        Assert.False(outside.ShowContactBlock);
        var self = await svc.DetailAsync(optedOut.SubjectId, optedOut.SubjectId); // self-view
        Assert.NotNull(self.Profile);
        Assert.True(self.ShowContactBlock);
        Assert.Equal(0, await RowCount(store, optedOut.SubjectId));

        // Shape 2 — audience excluding the owner: outside viewer Denies (one row,
        // unchanged §2.4); the owner's self-view allows and commits no additional
        // audit row — the count stays at exactly the outside viewer's one Deny.
        var denied = await svc.DetailAsync(otherViewer, ownerExcluding.SubjectId);
        Assert.NotNull(denied.Profile);
        Assert.False(denied.ShowContactBlock);
        Assert.Single(await OutcomeRowCount(store, ownerExcluding.SubjectId, AccessOutcome.Deny));
        var ownerSelf = await svc.DetailAsync(ownerExcluding.SubjectId, ownerExcluding.SubjectId); // self-view
        Assert.NotNull(ownerSelf.Profile);
        Assert.True(ownerSelf.ShowContactBlock);
        Assert.Equal(1, await RowCount(store, ownerExcluding.SubjectId));
    }

    // ── Shared helpers (mirror `DirectoryServiceTests`'s `BootStoreAsync` shape) ──

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

    /// <summary>
    /// Counts <see cref="AccessAudit"/> rows for one target profile (the §2.4
    /// per-shape audit-row-count probe in test 1). Uses the same
    /// <c>await using</c> + <c>CountAsync</c> shape as the inline probes
    /// elsewhere in this file and in <c>UserInfoServiceTests</c>.
    /// </summary>
    private static async Task<int> RowCount(IDocumentStore store, string profileSubjectId)
    {
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .Where(a => a.TargetId == profileSubjectId && a.TargetKind == "directory")
            .CountAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Like <see cref="RowCount"/>, filtered to one <see cref="AccessOutcome"/> —
    /// the §2.4 "exactly one contact decision, and here is which outcome it
    /// recorded" probe.
    /// </summary>
    private static async Task<IReadOnlyList<AccessAudit>> OutcomeRowCount(
        IDocumentStore store, string profileSubjectId, AccessOutcome outcome)
    {
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .Where(a => a.TargetId == profileSubjectId && a.TargetKind == "directory")
            .Where(a => a.Outcome == outcome)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}

