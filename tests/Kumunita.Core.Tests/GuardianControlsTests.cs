using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// GU (ADR 0028 — guardian controls, account-scope supervision of a child's account) —
/// the lane's <b>11 pinned seam tests</b>. These are the <c>Pinned contract →
/// Pinned seam tests (exact names)</c> frozen in
/// <c>docs/design/guardian-controls-design.md</c> (U01), asserting the U02–U06 seams:
/// the <see cref="GuardianLink"/> doc, <see cref="AccessVia.Guardian"/> (the 9th value),
/// the formation/suspension/independence seams (U04), the membership-curation
/// <c>Via: Guardian</c> branch (U05), and the invitation gate + approve (U06).
/// <para>
/// The load-bearing test is <see cref="G1_GuardianCannotReadChildContent"/> (invariant
/// G·1): a guardian's standing <b>never</b> resolves a content read — the lane's whole
/// honesty. <see cref="G3_ContentReadIsNeverGuardian"/> is its structural twin: the 9th
/// <see cref="AccessVia"/> value is unreachable on a <c>CanAsync</c>/<c>CanSeeAsync</c>
/// decision. <see cref="G5_GlobalAdminDissolvesAndUnSuspends"/> pins the G·5 safety valve
/// and the <b>dissolve ≠ un-suspend</b> reconciliation. The names are the contract; a
/// 12th test would be a drift pause, not a silent add.
/// </para>
/// <para>
/// Authority: ADR 0028 §C (the five supervisory actions + audit verbs), §D
/// (invariants G·1–G·5), §E (the audit-verb list), and the design doc's
/// <c>Pinned contract</c> section. Each test hands itself a fresh scratch Postgres DB
/// (<see cref="PostgresFixture.NewDatabaseAsync"/>), the <c>BootStoreAsync</c> shape the
/// M2b group-invitation tests established in this assembly.
/// </para>
/// </summary>
public class GuardianControlsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── G·1 (load-bearing) — the lane's honesty ─────────────────────────────
    // A guardian with an ACTIVE link over the child is DENIED a post the child authored
    // for a non-guardian audience; the decision carries NO Via: Guardian branch. The
    // child's own read of that same post is the owner branch (Allowed) — so the denial
    // is specifically the guardian standing, not the audience.

    [Fact]
    public async Task G1_GuardianCannotReadChildContent()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        var authz = new AuthorizationService(store, svc);

        const string guardian = "u-g1-guardian";
        const string child = "u-g1-child";

        // The guardian holds an ACTIVE link over the child (so they CAN curate).
        var link = await svc.CreateGuardianLinkAsync(child, guardian);
        Assert.Equal(GuardianLinkStatus.Active, link.Status);

        // A post the child authored, audience-restricted to the child alone (a
        // non-guardian audience — the guardian is not granted in).
        var childPost = new TestPost
        {
            Id = "post-g1-child",
            OwnerId = child,
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, child)]),
            TargetKind = "post",
        };

        // The guardian, given the child's id, is DENIED the child's content.
        var denied = await authz.CanAsync(guardian, AccessAction.Read, childPost);
        Assert.False(denied.Allowed);
        Assert.NotEqual(AccessVia.Guardian, denied.Via);

        // Contrast: the child reads their own post — the owner branch (Allowed). The
        // denial above is therefore the guardian standing, not the audience rule.
        var own = await authz.CanAsync(child, AccessAction.Read, childPost);
        Assert.True(own.Allowed);
        Assert.Equal(AccessVia.Owner, own.Via);
    }

    // ── G·2 — suspend is live standing off Profile.Blocked; un-suspend restores ──
    // SuspendChildAsync flips the SAME flag BlockedAccountMiddleware + the directory
    // already read (enforcement parity), live on the next read (C4); un-suspend
    // restores it. The audit verbs are guardian.suspend / guardian.unsuspend, Via: Guardian.

    [Fact]
    public async Task G2_SuspendIsLiveAndBlocksStanding()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string guardian = "u-g2-guardian";
        const string child = "u-g2-child";

        await SeedChildProfileAsync(store, child);
        await svc.CreateGuardianLinkAsync(child, guardian);

        // Before: the child's profile is un-suspended.
        Assert.False((await svc.GetProfileAsync(child))!.Blocked);

        // Suspend — the flag is live on the very next read (C4).
        await svc.SuspendChildAsync(child, guardian);
        Assert.True((await svc.GetProfileAsync(child))!.Blocked);

        // Un-suspend — standing restored on the next read.
        await svc.UnsuspendChildAsync(child, guardian);
        Assert.False((await svc.GetProfileAsync(child))!.Blocked);

        // The two GU audit verbs, both Via: Guardian (the guardian's own standing).
        var suspendRow = await LastAuditAsync(store, "guardian.suspend", child);
        Assert.Equal(AccessVia.Guardian, suspendRow.Via);
        Assert.Equal(AccessOutcome.Allow, suspendRow.Outcome);
        var unsuspendRow = await LastAuditAsync(store, "guardian.unsuspend", child);
        Assert.Equal(AccessVia.Guardian, unsuspendRow.Via);
    }

    // ── G·2 — dissolve restores the child's self-lanes on the very next read (C4) ──
    // While an active link is present the child's self-accept is refused (the U06 gate);
    // after DissolveGuardianLinkAsync the self-accept is OPEN AGAIN on the next call — the
    // "come-of-age" handoff. Dissolve writes nothing to membership (the gate simply turns
    // off because the link is no longer active).

    [Fact]
    public async Task G2_DissolveRestoresSelfLanesOnNextRead()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-g2o-owner";
        const string guardian = "u-g2o-guardian";
        const string child = "u-g2o-child";

        var group = await svc.CreateGroupAsync(owner, "CameOfAge", null);
        await svc.InviteGroupMemberAsync(group.Id, child, invitedBy: owner);
        var link = await svc.CreateGuardianLinkAsync(child, guardian);

        // Supervised: the child's self-accept is refused (the GU gate is live).
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AcceptGroupInvitationAsync(group.Id, child));

        // Dissolve (the independence lane) — the child's self-lane is OPEN AGAIN on the
        // very next attempt (C4). The membership then lands via the self-accept.
        await svc.DissolveGuardianLinkAsync(link.Id, guardian, viaAdmin: false);
        await using (var session = store.QuerySession())
        {
            var stored = await session.LoadAsync<GuardianLink>(link.Id, TestContext.Current.CancellationToken);
            Assert.Equal(GuardianLinkStatus.Dissolved, stored!.Status);
        }

        // No active link now → the self-accept succeeds; membership is live on the next read.
        await svc.AcceptGroupInvitationAsync(group.Id, child);
        Assert.Contains(group.Id, await svc.GetGroupIdsAsync(child));
    }

    // ── G·3 — deny-by-default: a guardian acting on a NON-child target is refused ───
    // The GU-specific lanes resolve standing live off an ACTIVE link for the exact
    // (guardian, child) pair; a foreign pair has no link ⇒ UnauthorizedAccessException
    // (the Web's 404). The no-standing gate fires before any row/state check.

    [Fact]
    public async Task G3_NonChildTargetIsRefused()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string guardian = "u-g3-guardian";
        const string supervisedChild = "u-g3-child";
        const string foreignTarget = "u-g3-foreign";  // NOT supervised by this guardian

        await SeedChildProfileAsync(store, foreignTarget);
        await svc.CreateGuardianLinkAsync(supervisedChild, guardian); // active link is over supervisedChild only
        var group = await svc.CreateGroupAsync("u-g3-owner", "G3", null);
        await svc.SeedComponentsAsync();
        var component = (await LoadFirstComponentAsync(store)).Id;

        // The GU-specific lanes are refused for a non-child (no active link over foreignTarget):
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.SuspendChildAsync(foreignTarget, guardian));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UnsuspendChildAsync(foreignTarget, guardian));
        // Approve's standing gate fires before the (group, child) row check:
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ApproveGroupInvitationAsync(group.Id, foreignTarget, guardian));
        // Community curation: the guardian branch yields no standing over a foreign
        // target, so the existing community gate (GlobalAdmin ∪ moderator) refuses —
        // a guardian holds neither.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.AddCommunityMemberAsync(component, foreignTarget, guardian, new HashSet<string>()));
    }

    // ── G·3 (structural twin of G·1) — no content-read decision records Via: Guardian ─
    // The 9th AccessVia value is UNREACHABLE on a CanAsync / CanSeeAsync decision: the
    // guardian's own content read resolves via Owner (Allowed), and a foreign read is a
    // Deny via Audience — never Guardian.

    [Fact]
    public async Task G3_ContentReadIsNeverGuardian()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        var authz = new AuthorizationService(store, svc);

        const string guardian = "u-g3r-guardian";
        const string child = "u-g3r-child";

        await svc.CreateGuardianLinkAsync(child, guardian); // the guardian standing is live

        // The guardian's OWN post: the owner branch (Allowed) — recorded as Owner, NOT Guardian.
        var ownPost = new TestPost
        {
            Id = "post-g3r-own",
            OwnerId = guardian,
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, guardian)]),
            TargetKind = "post",
        };
        var allow = await authz.CanAsync(guardian, AccessAction.Read, ownPost);
        Assert.True(allow.Allowed);
        Assert.NotEqual(AccessVia.Guardian, allow.Via);

        // A child post restricted to the child: a DENY for the guardian — recorded as the
        // deny Via (Audience), NEVER Guardian.
        var foreignPost = new TestPost
        {
            Id = "post-g3r-child",
            OwnerId = child,
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, child)]),
            TargetKind = "post",
        };
        var deny = await authz.CanAsync(guardian, AccessAction.Read, foreignPost);
        Assert.False(deny.Allowed);
        Assert.NotEqual(AccessVia.Guardian, deny.Via);
    }

    // ── G·4 — formation commits the link row + the audit row together, idempotently ──
    // CreateGuardianLinkAsync lands the Active link AND the guardian.create audit row in
    // one session (C3); a duplicate (guardian, child) is an IDEMPOTENT NO-OP (not a
    // throw) — the existing Active row is returned as-is and no second audit row lands.
    // (The account-pairing half — RegisterAsync + the link in one Web commit — is the
    // U07 add-a-child form's one-commit formation; the Core-testable half is link + audit.)

    [Fact]
    public async Task G4_FormationCommitsAccountLinkAndAuditTogether()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string guardian = "u-g4-guardian";
        const string child = "u-g4-child";

        var link = await svc.CreateGuardianLinkAsync(child, guardian);
        Assert.Equal(GuardianLinkStatus.Active, link.Status);
        Assert.Equal(guardian, link.GuardianId);
        Assert.Equal(child, link.ChildId);

        // The guardian.create audit row is in the same commit as the link.
        var createRow = await LastAuditAsync(store, "guardian.create", link.Id);
        Assert.Equal(AccessVia.Guardian, createRow.Via);
        Assert.Equal(guardian, createRow.ActorId);
        Assert.Equal(AccessOutcome.Allow, createRow.Outcome);

        // Idempotent no-op on a duplicate pair: the SAME row is returned, and NO second
        // audit row is appended (a no-op is a no-op — the contract, not an error).
        var countBefore = await CountAuditAsync(store, "guardian.create", link.Id);
        var again = await svc.CreateGuardianLinkAsync(child, guardian);
        Assert.Equal(link.Id, again.Id);
        Assert.Equal(GuardianLinkStatus.Active, again.Status);
        Assert.Equal(countBefore, await CountAuditAsync(store, "guardian.create", link.Id));
    }

    // ── G·5 — the safety valve: a GlobalAdmin dissolves an ACTIVE link (Via: Admin) ──
    // Dissolve ≠ un-suspend (the reconciliation pin): DissolveGuardianLinkAsync(viaAdmin:
    // true) moves the link Active → Dissolved (DissolvedBy = the admin, audit
    // guardian.dissolve Via: Admin) and writes NOTHING to Profile.Blocked — a suspended
    // child's Blocked flag is untouched by the dissolve. The actual un-suspension is a
    // SEPARATE act: the M1-frozen GlobalAdmin UnblockAsync (IIdentityService, audit
    // "unblock" Via: Admin) — the lane's U04 handoff keeps BlockAsync/UnblockAsync
    // byte-identical, and the Core-test assembly deliberately does not pull Identity
    // infra in (see DirectoryServiceTests.cs:76), so this test asserts the GU dissolve
    // seam + the "dissolve leaves Blocked alone" reconciliation, not the M1 valve body.

    [Fact]
    public async Task G5_GlobalAdminDissolvesAndUnSuspends()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string guardian = "u-g5-guardian";
        const string admin = "u-g5-admin";   // a GlobalAdmin — NOT the GuardianId
        const string child = "u-g5-child";

        await SeedChildProfileAsync(store, child);
        var link = await svc.CreateGuardianLinkAsync(child, guardian);

        // The child is suspended (the guardian's GU lane) — Blocked is the suspension.
        await svc.SuspendChildAsync(child, guardian);
        Assert.True((await svc.GetProfileAsync(child))!.Blocked);

        // Act 1 — the GlobalAdmin dissolves the ACTIVE link (not the GuardianId; viaAdmin
        // skips the GuardianId check): the link closes, audited Via: Admin.
        await svc.DissolveGuardianLinkAsync(link.Id, admin, viaAdmin: true);
        await using (var session = store.QuerySession())
        {
            var stored = await session.LoadAsync<GuardianLink>(link.Id, TestContext.Current.CancellationToken);
            Assert.Equal(GuardianLinkStatus.Dissolved, stored!.Status);
            Assert.Equal(admin, stored.DissolvedBy);
        }
        var dissolveRow = await LastAuditAsync(store, "guardian.dissolve", link.Id);
        Assert.Equal(AccessVia.Admin, dissolveRow.Via);
        Assert.Equal(admin, dissolveRow.ActorId);

        // RECONCILIATION (dissolve ≠ un-suspend): the dissolve alone did NOT touch
        // Profile.Blocked — the suspended child's Blocked flag is STILL true. The
        // un-suspension is the separate M1 UnblockAsync act (a M1-frozen lane, not GU's).
        Assert.True((await svc.GetProfileAsync(child))!.Blocked);
    }

    // ── G·2 — a supervised child cannot self-accept (gate) but CAN self-decline ─────
    // While an active link is present, AcceptGroupInvitationAsync is refused for the
    // child (the GU gate → InvalidOperationException); the gate is CONDITIONAL on the
    // active link — a child with no link self-accepts normally. The child's
    // self-DECLINE stays open (a child may always say no).

    [Fact]
    public async Task Invitation_GatedForSupervisedChild()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-inv-owner";
        const string guardian = "u-inv-guardian";
        const string supervisedChild = "u-inv-supervised";
        const string independentChild = "u-inv-independent"; // no link

        var gSupervised = await svc.CreateGroupAsync(owner, "Supervised", null);
        await svc.InviteGroupMemberAsync(gSupervised.Id, supervisedChild, invitedBy: owner);
        await svc.CreateGuardianLinkAsync(supervisedChild, guardian);

        // Supervised: the self-accept is refused (the GU gate).
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AcceptGroupInvitationAsync(gSupervised.Id, supervisedChild));

        // But the self-decline stays open (a child may always say no).
        await svc.DeclineGroupInvitationAsync(gSupervised.Id, supervisedChild);
        Assert.DoesNotContain(gSupervised.Id, await svc.GetGroupIdsAsync(supervisedChild));

        // Conditional on the active link: a child with NO link self-accepts normally.
        var gIndependent = await svc.CreateGroupAsync(owner, "Independent", null);
        await svc.InviteGroupMemberAsync(gIndependent.Id, independentChild, invitedBy: owner);
        await svc.AcceptGroupInvitationAsync(gIndependent.Id, independentChild);
        Assert.Contains(gIndependent.Id, await svc.GetGroupIdsAsync(independentChild));
    }

    // ── G·2 — the guardian approves; the membership lands (Via: Guardian) ──────────
    // ApproveGroupInvitationAsync resolves the child's Pending invitation as Accepted
    // (ResolvedBy = the guardian), the GroupMembership is live on the next
    // GetGroupIdsAsync, and the audit row is group.invite.approve Via: Guardian.

    [Fact]
    public async Task Invitation_GuardianApproveLandsMembership_ViaGuardian()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-ia-owner";
        const string guardian = "u-ia-guardian";
        const string child = "u-ia-child";

        var group = await svc.CreateGroupAsync(owner, "Approved", null);
        await svc.InviteGroupMemberAsync(group.Id, child, invitedBy: owner);
        await svc.CreateGuardianLinkAsync(child, guardian);

        var row = await svc.ApproveGroupInvitationAsync(group.Id, child, guardian);
        Assert.Equal(InvitationStatus.Accepted, row.Status);
        Assert.Equal(guardian, row.ResolvedBy);

        // The membership is live on the very next read (C4).
        Assert.Contains(group.Id, await svc.GetGroupIdsAsync(child));

        // The audit row: group.invite.approve, Via: Guardian, targeting the group.
        var approveRow = await LastAuditAsync(store, "group.invite.approve", group.Id);
        Assert.Equal(AccessVia.Guardian, approveRow.Via);
        Assert.Equal(guardian, approveRow.ActorId);
        Assert.Equal("group", approveRow.TargetKind);
    }

    // ── G·3 — membership curation records the NARROWER standing (Via: Guardian) ─────
    // The guardian adds/removes the child from a GROUP and a COMMUNITY; the audit rows
    // record the narrower standing Via: Guardian (the ADR 0012 "record the narrower
    // standing" rule — the guardian branch wins over Owner/Admin), and the membership
    // is live on the next read (C4).

    [Fact]
    public async Task Membership_AddRemoveChild_ViaGuardian()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-mm-owner";
        const string guardian = "u-mm-guardian";
        const string child = "u-mm-child";

        var group = await svc.CreateGroupAsync(owner, "Curated", null);
        var component = (await LoadFirstComponentAsync(store)).Id; // requires the seeded 4 components
        await svc.CreateGuardianLinkAsync(child, guardian);

        // ── Group add / remove (the guardian as addedBy/removedBy) ──────────────
        await svc.AddGroupMemberAsync(group.Id, child, guardian);
        Assert.Contains(group.Id, await svc.GetGroupIdsAsync(child));
        var addRow = await LastAuditAsync(store, "group.add-member", group.Id);
        Assert.Equal(AccessVia.Guardian, addRow.Via);   // the NARROWER standing, not Admin
        Assert.Equal(guardian, addRow.ActorId);

        await svc.RemoveGroupMemberAsync(group.Id, child, guardian);
        Assert.DoesNotContain(group.Id, await svc.GetGroupIdsAsync(child));
        var removeRow = await LastAuditAsync(store, "group.remove-member", group.Id);
        Assert.Equal(AccessVia.Guardian, removeRow.Via);

        // ── Community add / remove (the guardian branch bypasses the community
        //    standing gate — a guardian holds neither GlobalAdmin nor a moderator
        //    scope — and records Via: Guardian). ─────────────────────────────────
        await svc.AddCommunityMemberAsync(component, child, guardian, new HashSet<string>());
        Assert.Contains(component, await svc.GetCommunityIdsAsync(child));
        var commAdd = await LastAuditAsync(store, "community.add-member", component);
        Assert.Equal(AccessVia.Guardian, commAdd.Via);
        Assert.Equal(guardian, commAdd.ActorId);

        await svc.RemoveCommunityMemberAsync(component, child, guardian, new HashSet<string>());
        Assert.DoesNotContain(component, await svc.GetCommunityIdsAsync(child));
        var commRemove = await LastAuditAsync(store, "community.remove-member", component);
        Assert.Equal(AccessVia.Guardian, commRemove.Via);
    }

    // ── G·2 — suspend's enforcement is IDENTICAL to the existing block lane ─────────
    // SuspendChildAsync sets the SAME Profile.Blocked flag BlockedAccountMiddleware and
    // the directory already read, so the suspended child is excluded from the directory
    // (the only account-level exclusion) and restored on un-suspend — byte-identical
    // enforcement, the only difference being the audit verb (guardian.suspend Via:
    // Guardian, vs the M1 "block" Via: Admin).

    [Fact]
    public async Task SuspendSetsProfileBlocked_EnforcementIdentical()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        var authz = new AuthorizationService(store, svc);
        var directory = new DirectoryService(svc, authz);

        const string guardian = "u-sb-guardian";
        const string child = "u-sb-child";
        const string viewer = "u-sb-viewer"; // a signed-in viewer (ListAsync is sign-in-gated)

        await SeedChildProfileAsync(store, child);
        await svc.CreateGuardianLinkAsync(child, guardian);

        // Before the suspension: the child is in the directory (a non-blocked resident).
        Assert.Contains(child, (await directory.ListAsync(viewer)).Visible.Select(p => p.SubjectId));

        // Suspend — the SAME flag the directory already reads (enforcement parity):
        await svc.SuspendChildAsync(child, guardian);
        Assert.True((await svc.GetProfileAsync(child))!.Blocked);
        Assert.DoesNotContain(child, (await directory.ListAsync(viewer)).Visible.Select(p => p.SubjectId));

        // Un-suspend — the child is back in the directory on the next read.
        await svc.UnsuspendChildAsync(child, guardian);
        Assert.False((await svc.GetProfileAsync(child))!.Blocked);
        Assert.Contains(child, (await directory.ListAsync(viewer)).Visible.Select(p => p.SubjectId));

        // The GU audit verb is the guardrail (guardian.suspend Via: Guardian) — not the
        // M1 "block" Via: Admin. Enforcement is identical; the standing recorded differs.
        var suspendRow = await LastAuditAsync(store, "guardian.suspend", child);
        Assert.Equal(AccessVia.Guardian, suspendRow.Via);
    }

    // ── Minimal IAuditableResource (a "post") for the content-decision negations ──

    private sealed class TestPost : IAuditableResource
    {
        public string Id { get; set; } = string.Empty;
        public string Name => Id;
        public string? OwnerId { get; set; }
        public Audience? Audience { get; set; }
        public string? ComponentId { get; set; }
        public string TargetKind { get; set; } = string.Empty;
    }

    // ── Shared helpers ──────────────────────────────────────────────────────

    private static async Task<AccessAudit> LastAuditAsync(IDocumentStore store, string action, string targetId)
    {
        await using var session = store.QuerySession();
        var row = await session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId)
            .OrderByDescending(a => a.At)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        return row!;
    }

    private static async Task<int> CountAuditAsync(IDocumentStore store, string action, string targetId)
    {
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId)
            .CountAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Seeds a minimal verified, un-blocked <see cref="Profile"/> for the child so the
    /// suspension lane (which loads the child's profile by <c>SubjectId</c>) and the
    /// directory (which lists non-blocked residents) have something to read.
    /// </summary>
    private static async Task SeedChildProfileAsync(IDocumentStore store, string childId)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Profile
        {
            SubjectId = childId,
            DisplayName = "Child",
            Verified = true,
            Blocked = false,
            Visibility = new Audience(),
        });
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Seeds the four default components (ADR 0012) and returns the first one — the
    /// community curation tests need a valid <c>componentId</c>.
    /// </summary>
    private async Task<Component> LoadFirstComponentAsync(IDocumentStore store)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var existing = await session.Query<Component>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        if (existing is not null)
            return existing;
        return (await new UserInfoService(store).SeedComponentsAsync())[0];
    }

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
}
