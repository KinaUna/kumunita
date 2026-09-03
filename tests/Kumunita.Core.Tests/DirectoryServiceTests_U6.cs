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
    // ── Test 1 — ContactVisibility_FourShape_TrightTable (§2.4, C-M2·1, §9 pin) ─
    //
    // The design doc §2.4's four-shape truth table, each cell with a `Visibility`
    // that *already allowed* the target (the gating precondition — otherwise the
    // whole table never runs, F4). For each shape we assert the returned
    // `ShowContactBlock` AND the exact audit-row count for that profile:
    //   • row 1 (`null`) — the contact decision is *not evaluated* (C-M2·1 / §2.4
    //     literal "not evaluated"): exactly ONE audit row exists (visibility only);
    //     a null contact audience must not be mis-modeled as a Deny (the §9 pin in
    //     its "short-circuit, not a denial, not an evaluation" form).
    //   • row 2 (`Any` + empty grants) — C1 empty-audience guard: exactly TWO audit
    //     rows (the §2.4 "separate-call" pin — it *was* a distinct second decision,
    //     a C6-compliant shared-matching-pass call), and the contact one is a Deny.
    //     `ShowContactBlock == false`.
    //   • row 3 (`Any` + grant, viewer in-grant) — evaluates through
    //     `<c>MatchGroups</c>`; exactly two rows, the contact one is an Allow.
    //     `ShowContactBlock == true`.
    //   • row 4 (`All` + empty grants) — C1 `All` + empty denies (the vacuous-truth
    //     guard this invariant exists for): exactly two rows, the contact one is a
    //     Deny. `ShowContactBlock == false`.
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

        // Row 1 — `null` short-circuit: visible; contact NOT evaluated, no contact
        // audit row (the §9 pin — a null contact audience is not a Deny and not an
        // evaluation; it is an early return before the second `CanAsync` call).
        var d1 = await svc.DetailAsync(viewer, rowNull.SubjectId);
        Assert.True(d1!.IsVisible);
        Assert.False(d1.ShowContactBlock);
        // Row 1 short-circuit pin: exactly one audit row (visibility only) — the
        // `null` ContactVisibility produced no second decision / audit row (§9).
        Assert.Equal(1, await RowCount(store, rowNull.SubjectId));

        // Row 2 — `Any` + empty: evaluates (a separate decision — the §2.4 pin),
        // Deny (C1 empty-audience guard), no contact rendered.
        var d2 = await svc.DetailAsync(viewer, rowAnyEmpty.SubjectId);
        Assert.True(d2!.IsVisible);
        Assert.False(d2.ShowContactBlock);
        // Two rows: the contact decision was a distinct 2nd decision (the §2.4
        // separate-call / C6 pin) even though it denied.
        Assert.Equal(2, await RowCount(store, rowAnyEmpty.SubjectId));
        Assert.Single(await OutcomeRowCount(store, rowAnyEmpty.SubjectId, AccessOutcome.Deny));

        // Row 3 — `Any` + grant the viewer is in: evaluates through MatchGroups,
        // Allow, contact rendered.
        var d3 = await svc.DetailAsync(viewer, rowAnyGrant.SubjectId);
        Assert.True(d3!.IsVisible);
        Assert.True(d3.ShowContactBlock);
        Assert.Equal(2, await RowCount(store, rowAnyGrant.SubjectId));

        // Row 4 — `All` + empty: evaluates, Deny (the vacuous-truth guard, C1).
        var d4 = await svc.DetailAsync(viewer, rowAllEmpty.SubjectId);
        Assert.True(d4!.IsVisible);
        Assert.False(d4.ShowContactBlock);
        Assert.Equal(2, await RowCount(store, rowAllEmpty.SubjectId));
        Assert.Single(await OutcomeRowCount(store, rowAllEmpty.SubjectId, AccessOutcome.Deny));
    }

    // ── Test 2 — Unverified_SelfCandidate_NotAudited (C-M2·2, §4.3/§2.3) ───
    //
    // U5's own self-check (`ListAsync_Hides_Unverified`, same-assembly
    // `DirectoryServiceTests`) pins the *visible-set shape* (exact viewer-own row,
    // `HiddenCount = 0`, no other resident named anywhere in any row). U6's
    // addition — the plan's exact wording, "the audit row is a *single*
    // Owner-branch `Read` on the viewer's *own* profile (not a `Visibility`/
    // `ContactVisibility` audit)" — pins the *audit-row shape itself* at the
    // aggregate-row and per-item-row level: Action/TargetKind/Via/Outcome/
    // EffectivePrincipalId asserted explicitly on both (the C-M2·2 §2.3 pin — the
    // candidate filter produced exactly this one, owner-branch decision, and never
    // a `ContactVisibility`/visibility decision *row* naming the excluded other).

    [Fact]
    public async Task Unverified_SelfCandidate_NotAudited()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new DirectoryService(userInfo, authz);

        const string verifiedOther = "u-u6-t2-verified-other";
        const string unverifiedViewer = "u-u6-t2-unverified-viewer";

        // A verified resident whose *own* audience would allow the viewer — so the
        // test proves the result is driven by the §2.3 *filter*, not by audience
        // evaluation: if the viewer had reached `CanSeeAsync` with both candidates
        // (the filter's job is to have excluded this one), this profile would have
        // surfaced in `Visible` and its subject id would have appeared in at least
        // one audit row.
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

        var result = await svc.ListAsync(unverifiedViewer, viewerVerified: false);

        // Same shape pin U5 owns (single self row, zero hidden, other never appears).
        Assert.Single(result.Visible);
        Assert.Equal(unverifiedViewer, result.Visible[0].SubjectId);
        Assert.Equal(0, result.HiddenCount);

        // The §2.3 / C-M2·2 audit pin, at the row level: exactly the two rows of a
        // *single* 1-candidate bulk Read by the viewer on their own profile — one
        // aggregate row (C3: one per bulk decision) + one per-item row (C3: one per
        // audience-restricted visible candidate). Both Owner-via (the owner branch
        // is branch 1 of the §4.4 algorithm and fires before `MatchGroups`), both
        // Allow, both with the viewer as their effective principal (no delegation,
        // no moderator standing planted here), and the action/target kind pinned to
        // `read` / `directory` — never a `ContactVisibility` or other resource shape.
        await using var session = store.QuerySession();
        var audited = await session.Query<AccessAudit>()
            .Where(a => a.Action == AccessAction.Read.Id
                        && a.TargetKind == "directory"
                        && a.ActorId == unverifiedViewer)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, audited.Count);

        var aggregate = audited.Single(a => a.TargetId is null);
        Assert.Equal(AccessVia.Owner, aggregate.Via);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);
        Assert.Equal(1, aggregate.VisibleCount);
        Assert.Equal(0, aggregate.HiddenCount);
        Assert.Equal(unverifiedViewer, aggregate.EffectivePrincipalId);

        var perItem = audited.Single(a => a.TargetId is not null);
        Assert.Equal(unverifiedViewer, perItem.TargetId);
        Assert.Equal(AccessVia.Owner, perItem.Via);
        Assert.Equal(AccessOutcome.Allow, perItem.Outcome);
        Assert.Equal(unverifiedViewer, perItem.EffectivePrincipalId);

        // C-M2·2's DB-level pin: the excluded verified resident appears NOWHERE
        // (not as actor, not as effective principal, not as target) in *any* audit
        // row — evidence the §2.3 filter excluded them before `CanSeeAsync` could
        // name them in any row, and no `ContactVisibility`-or-other decision ran
        // on their row.
        await using var auditSession = store.QuerySession();
        var allRows = await auditSession.Query<AccessAudit>()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(allRows, a =>
        {
            Assert.NotEqual(verifiedOther, a.ActorId);
            Assert.NotEqual(verifiedOther, a.EffectivePrincipalId);
            Assert.NotEqual(verifiedOther, a.TargetId);
        });
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
        Assert.Equal(singleDeny.Via, AccessVia.Audience);
        Assert.Equal(singleDeny.EffectivePrincipalId, other);

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
    // The plan's exact wording — "C4 for *profiles' own audiences*: a member add
    // in the same commit is visible to `ListAsync` on the next call (M1's
    // `MembershipTests` pattern, now applied to the `Profile` audience shape)" —
    // exercised on the *directory* surface (`DirectoryService.ListAsync`, U5's
    // composition), over a `Profile` whose `Visibility` is a `Group`-grant (the
    // "reuse unit" F1 named), and with the `AddGroupMemberAsync` /
    // `RemoveGroupMemberAsync` audit rows' own shape (C3, and C-M2·3's SoD pin —
    // the `ActorId` is the *owner*, the only standing that reaches those methods,
    // per the interface's own docs):
    //   (a) pre-add: the member's `ListAsync` is exactly 1 visible (themselves,
    //       via the owner branch) + 1 hidden (the owner's group-scoped profile);
    //       the owner's profile is *not* in `Visible`.
    //   (b) `AddGroupMemberAsync`'s own `AccessAudit` row: `Allow`/`Via = Owner`/
    //       `TargetKind = "group"`/`ActorId = owner` (C3 same-transaction shape;
    //       C-M2·3 SoD — the owner is the grantor, per the signature).
    //   (c) post-add: the *very next* `ListAsync` is 2 visible + 0 hidden (C4 /
    //       F2 live-on-next-call — the interface doc's own "live on the next
    //       GetGroupIdsAsync call" wording, here as the next *list* decision),
    //       and the owner's profile is now in `Visible`.
    //   (d) `RemoveGroupMemberAsync`'s own `AccessAudit` row, same shape pin; then
    //       the very next `ListAsync` is back to 1 visible + 1 hidden — "the loss
    //       of access is live on the next ... call", per the interface doc.

    [Fact]
    public async Task GroupAddRemoveMember_ReflectedOnNext_Call_Profile()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new DirectoryService(userInfo, authz);

        const string owner = "u-u6-t5-owner";
        const string member = "u-u6-t5-member";

        // Owner's profile: group-scoped audience (a `Group` grant — the "reuse
        // unit" F1 named) — only group members can see it.
        var group = await userInfo.CreateGroupAsync(owner, "U6 T5 group", null);
        await userInfo.UpsertProfileAsync(
            new Profile
            {
                SubjectId = owner,
                DisplayName = "Owner",
                Verified = true,
                Visibility = new Audience(AudienceMode.Any,
                    [new AudienceGrant(GrantKind.Group, group.Id)]),
            },
            new ProfileUpdate(null, null, null, null, null));

        // The member's own profile: a self-grant (their own `ListAsync` always
        // includes themselves via the owner branch — the "1 visible" baseline for
        // the pre-add and post-remove states below).
        await userInfo.UpsertProfileAsync(
            new Profile
            {
                SubjectId = member,
                DisplayName = "Member",
                Verified = true,
                Visibility = new Audience(AudienceMode.Any,
                    [new AudienceGrant(GrantKind.User, member)]),
            },
            new ProfileUpdate(null, null, null, null, null));

        // (a) Pre-add: the member's `ListAsync` includes themselves + exactly one
        // hidden profile (the owner's group-scoped one, denied by the group-scoped
        // audience).
        var before = await svc.ListAsync(member, viewerVerified: true);
        Assert.Equal(1, before.Visible.Count);
        Assert.Equal(1, before.HiddenCount);
        Assert.Contains(before.Visible, p => p.SubjectId == member);
        Assert.DoesNotContain(before.Visible, p => p.SubjectId == owner);

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

        // (c) Post-add: live on the very next `ListAsync` (C4/F2). 2 visible
        // (self + owner), 0 hidden.
        var afterAdd = await svc.ListAsync(member, viewerVerified: true);
        Assert.Equal(2, afterAdd.Visible.Count);
        Assert.Equal(0, afterAdd.HiddenCount);
        Assert.Contains(afterAdd.Visible, p => p.SubjectId == member);
        Assert.Contains(afterAdd.Visible, p => p.SubjectId == owner);

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

        // (e) The loss of access is live on the very next `ListAsync` (C4/F2, the
        // `RemoveGroupMemberAsync` interface doc's own wording).
        var afterRemove = await svc.ListAsync(member, viewerVerified: true);
        Assert.Equal(1, afterRemove.Visible.Count);
        Assert.Equal(1, afterRemove.HiddenCount);
        Assert.Contains(afterRemove.Visible, p => p.SubjectId == member);
        Assert.DoesNotContain(afterRemove.Visible, p => p.SubjectId == owner);
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

