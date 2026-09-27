using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0094 (docs/adr/0094-group-join-request-lane.md) — the resident
/// self-initiated <b>join-request</b> lane on <see cref="UserInfoService"/>:
/// the reverse direction of the m2b owner-invited lane. The <i>resident</i>
/// starts it (request + withdraw self-lane) and the group's owner ∪ GlobalAdmin
/// resolves it (approve → membership lands / decline). Each test hands itself a
/// fresh scratch Postgres DB (<see cref="PostgresFixture.NewDatabaseAsync"/>),
/// mirroring the <c>BootStoreAsync</c> shape <c>UserInfoServiceGroupsU9Tests</c>
/// and <c>UserInfoServiceGroupInvitationsM2bTests</c> established here.
/// <para>
/// Coverage:
/// </para>
/// <list type="bullet">
/// <item>JR·1 — the SoD lane: approve/decline by the group's owner ⇒
/// <see cref="AccessVia.Owner"/>; by a non-owner (GlobalAdmin standing) ⇒
/// <see cref="AccessVia.Admin"/>; each write appends its <see cref="AccessAudit"/>
/// row in the same transaction (invariant C3) with <c>TargetKind</c> "group".</item>
/// <item>JR·2 — the self-lane: only the requester (actor == row.UserId) may
/// withdraw their own row; a foreign caller throws <see cref="InvalidOperationException"/>.
/// Request + withdraw always audit under the requester's own standing
/// (<see cref="AccessVia.Owner"/>, all three identities the requester).</item>
/// <item>JR·3 — the state machine: a resolved (Approved / Declined) row is an
/// invalid transition for approve / decline / withdraw; re-requesting resets a
/// resolved row back to <c>Pending</c> (no second row — one per (group, user)
/// business key, <see cref="M1DocTypes"/>'s <c>UniqueIndex</c>).</item>
/// <item>C4 carried — approve makes the membership live on the <b>very next</b>
/// <see cref="IUserInfoService.GetGroupIdsAsync"/> /
/// <see cref="IUserInfoService.GetGroupsForUserAsync"/> call; a request and a
/// decline never touch membership.</item>
/// <item>C-M2·2 carried — the two read lanes
/// (<c>GetPendingJoinRequestsForUserAsync</c>,
/// <c>GetPendingJoinRequestsForGroupAsync</c>) append no <see cref="AccessAudit"/>
/// row.</item>
/// <item>GU gate carried — a supervised child (an active <see cref="GuardianLink"/>)
/// is refused on the approve lane, exactly as
/// <c>AcceptGroupInvitationAsync</c> (ADR 0028 §C / G·2).</item>
/// <item>DeleteGroupAsync cascade — the group's join-request rows are deleted
/// alongside its invitations when the group is deleted.</item>
/// </list>
/// </summary>
public class UserInfoServiceGroupJoinRequestTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Lifecycle 1 — request, then approve: membership lands ONLY on approve ──

    [Fact]
    public async Task Request_Then_Approve_Membership_LiveOnNextCall_C4()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-owner";
        const string requester = "u-jr-requester";

        var group = await svc.CreateGroupAsync(owner, "Building 4", null);

        // Request: the pending row exists, but NO membership yet (the C4 lane
        // is approve-only — a request itself never touches membership).
        await svc.RequestToJoinGroupAsync(group.Id, requester);
        Assert.DoesNotContain(group.Id, await svc.GetGroupIdsAsync(requester));
        Assert.DoesNotContain(group.Id, (await svc.GetGroupsForUserAsync(requester)).Select(g => g.Id));

        // Approve (the owner ∪ GlobalAdmin lane): the membership is live on the
        // very next read (C4 carried).
        await svc.ApproveJoinRequestAsync(group.Id, requester, owner);
        Assert.Contains(group.Id, await svc.GetGroupIdsAsync(requester));
        Assert.Contains(group.Id, (await svc.GetGroupsForUserAsync(requester)).Select(g => g.Id));
    }

    // ── Lifecycle 2 — decline: no membership, row resolved ─────────────────

    [Fact]
    public async Task Request_Then_Decline_NoMembership_RowResolved()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-a";
        const string requester = "u-jr-b";

        var group = await svc.CreateGroupAsync(owner, "Volunteers", null);
        await svc.RequestToJoinGroupAsync(group.Id, requester);

        await svc.DeclineJoinRequestAsync(group.Id, requester, owner);

        Assert.DoesNotContain(group.Id, await svc.GetGroupIdsAsync(requester));

        // The row resolved — it is no longer in the requester's pending list,
        // nor the group's pending list.
        Assert.Empty(await svc.GetPendingJoinRequestsForUserAsync(requester));
        Assert.Empty(await svc.GetPendingJoinRequestsForGroupAsync(group.Id));
    }

    // ── Lifecycle 3 — withdraw: the requester pulls their own pending row ────

    [Fact]
    public async Task Request_Then_Withdraw_NoMembership_RowResolved()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-c";
        const string requester = "u-jr-d";

        var group = await svc.CreateGroupAsync(owner, "Bike owners", null);
        await svc.RequestToJoinGroupAsync(group.Id, requester);

        await svc.WithdrawJoinRequestAsync(group.Id, requester);

        Assert.DoesNotContain(group.Id, await svc.GetGroupIdsAsync(requester));
        Assert.Empty(await svc.GetPendingJoinRequestsForUserAsync(requester));
        Assert.Empty(await svc.GetPendingJoinRequestsForGroupAsync(group.Id));
    }

    // ── JR·1 — the SoD lane's via derivation (owner ⇒ Owner, else Admin) ─────

    [Fact]
    public async Task Approve_Via_Owner_When_Resolver_Is_GroupOwner()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-e";
        const string requester = "u-jr-f";

        var group = await svc.CreateGroupAsync(owner, "Building 12", null);
        await svc.RequestToJoinGroupAsync(group.Id, requester);
        await svc.ApproveJoinRequestAsync(group.Id, requester, owner);

        var row = await LastAuditAsync(store, "group.join.approve", group.Id);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(owner, row.ActorId);
        Assert.Equal(owner, row.EffectivePrincipalId);
    }

    [Fact]
    public async Task Approve_Via_Admin_When_Resolver_Is_Not_The_Owner()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-g";
        const string admin = "u-jr-admin";
        const string requester = "u-jr-h";

        var group = await svc.CreateGroupAsync(owner, "Building 7", null);
        await svc.RequestToJoinGroupAsync(group.Id, requester);
        await svc.ApproveJoinRequestAsync(group.Id, requester, admin);

        var row = await LastAuditAsync(store, "group.join.approve", group.Id);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(admin, row.ActorId);
        Assert.Equal(admin, row.EffectivePrincipalId);   // not the owner's standing
    }

    [Fact]
    public async Task Decline_AuditRow_Use_The_Resolver_Standing()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-i";
        const string admin = "u-jr-i-admin";
        const string requester1 = "u-jr-i-1";
        const string requester2 = "u-jr-i-2";

        var gOwner = await svc.CreateGroupAsync(owner, "A", null);
        var gAdmin = await svc.CreateGroupAsync(owner, "D", null);

        await svc.RequestToJoinGroupAsync(gOwner.Id, requester1);
        await svc.DeclineJoinRequestAsync(gOwner.Id, requester1, owner);

        await svc.RequestToJoinGroupAsync(gAdmin.Id, requester2);
        await svc.DeclineJoinRequestAsync(gAdmin.Id, requester2, admin);

        var ownerRow = await LastAuditAsync(store, "group.join.decline", gOwner.Id);
        Assert.Equal(AccessVia.Owner, ownerRow.Via);
        Assert.Equal(owner, ownerRow.ActorId);

        var adminRow = await LastAuditAsync(store, "group.join.decline", gAdmin.Id);
        Assert.Equal(AccessVia.Admin, adminRow.Via);
        Assert.Equal(admin, adminRow.ActorId);
    }

    [Fact]
    public async Task RequestAndWithdraw_AuditRows_Use_The_Requesters_Own_Standing()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-j";
        const string requester = "u-jr-k";

        var g1 = await svc.CreateGroupAsync(owner, "Req", null);
        var g2 = await svc.CreateGroupAsync(owner, "Withdraw", null);

        await svc.RequestToJoinGroupAsync(g1.Id, requester);
        var reqRow = await LastAuditAsync(store, "group.join.request", g1.Id);
        Assert.Equal(AccessVia.Owner, reqRow.Via);           // the requester's own standing
        Assert.Equal(requester, reqRow.ActorId);
        Assert.Equal(requester, reqRow.EffectivePrincipalId);

        await svc.RequestToJoinGroupAsync(g2.Id, requester);
        await svc.WithdrawJoinRequestAsync(g2.Id, requester);
        var wdRow = await LastAuditAsync(store, "group.join.withdraw", g2.Id);
        Assert.Equal(AccessVia.Owner, wdRow.Via);            // the requester's own standing
        Assert.Equal(requester, wdRow.ActorId);
        Assert.Equal(requester, wdRow.EffectivePrincipalId);
    }

    // ── JR·2 — the self-lane: a foreign caller cannot withdraw the row ───────

    [Fact]
    public async Task WithdrawBy_A_Foreign_Account_Throws_JR_2()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-m";
        const string requester = "u-jr-n";
        const string stranger = "u-jr-o";

        var group = await svc.CreateGroupAsync(owner, "Building 3", null);
        await svc.RequestToJoinGroupAsync(group.Id, requester);

        // A stranger (not the row's UserId) cannot withdraw the request.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.WithdrawJoinRequestAsync(group.Id, stranger));

        // ... and the row is still pending for the requester (nothing changed).
        Assert.Single(await svc.GetPendingJoinRequestsForUserAsync(requester));
    }

    [Fact]
    public async Task Withdraw_Unknown_Group_Or_Pair_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-p";
        const string requester = "u-jr-q";

        var group = await svc.CreateGroupAsync(owner, "Building 9", null);

        // No request row at all for this pair.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.WithdrawJoinRequestAsync(group.Id, requester));

        // A group id that does not exist.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.WithdrawJoinRequestAsync("no-such-group", requester));
    }

    // ── Approve/decline on a pair with no request row throws ─────────────────

    [Fact]
    public async Task ApproveOrDecline_No_RequestRow_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-r";
        const string requester = "u-jr-s";

        var group = await svc.CreateGroupAsync(owner, "Building 5", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ApproveJoinRequestAsync(group.Id, requester, owner));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeclineJoinRequestAsync(group.Id, requester, owner));

        // A group id that does not exist.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ApproveJoinRequestAsync("no-such-group", requester, owner));
    }

    // ── JR·3 — the state machine ─────────────────────────────────────────────

    [Fact]
    public async Task ReRequest_After_Decline_Resets_To_Pending_JR_3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-t";
        const string requester = "u-jr-u";

        var group = await svc.CreateGroupAsync(owner, "Building 6", null);

        await svc.RequestToJoinGroupAsync(group.Id, requester);
        await svc.DeclineJoinRequestAsync(group.Id, requester, owner);
        Assert.Empty(await svc.GetPendingJoinRequestsForUserAsync(requester));

        // Re-request: the SAME (group, user) business-key row resets to
        // Pending — no second row, and the requester is pending once.
        await svc.RequestToJoinGroupAsync(group.Id, requester);
        var pending = await svc.GetPendingJoinRequestsForUserAsync(requester);
        Assert.Single(pending);
        Assert.Equal(JoinRequestStatus.Pending, pending[0].Status);

        // And the approve lane works end-to-end on the re-requested row.
        await svc.ApproveJoinRequestAsync(group.Id, requester, owner);
        Assert.Contains(group.Id, await svc.GetGroupIdsAsync(requester));
    }

    [Fact]
    public async Task Requesting_The_Same_Pair_Twice_Keeps_One_Row_JR_3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-v";
        const string requester = "u-jr-w";

        var group = await svc.CreateGroupAsync(owner, "Building 8", null);

        await svc.RequestToJoinGroupAsync(group.Id, requester);
        await svc.RequestToJoinGroupAsync(group.Id, requester);

        // One row per (group, user) — the UniqueIndex business key; the
        // re-stamp, not a second row.
        Assert.Single(await svc.GetPendingJoinRequestsForUserAsync(requester));
        Assert.Single(await svc.GetPendingJoinRequestsForGroupAsync(group.Id));
    }

    [Fact]
    public async Task Resolved_Row_Cannot_Approve_Decline_Or_Withdraw_Again_JR_3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-x";
        const string requester1 = "u-jr-x1";
        const string requester2 = "u-jr-x2";
        const string requester3 = "u-jr-x3";

        var g1 = await svc.CreateGroupAsync(owner, "G1", null);
        var g2 = await svc.CreateGroupAsync(owner, "G2", null);
        var g3 = await svc.CreateGroupAsync(owner, "G3", null);

        // Approved row: a second approve (or a decline) is an invalid transition.
        await svc.RequestToJoinGroupAsync(g1.Id, requester1);
        await svc.ApproveJoinRequestAsync(g1.Id, requester1, owner);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ApproveJoinRequestAsync(g1.Id, requester1, owner));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeclineJoinRequestAsync(g1.Id, requester1, owner));

        // Declined row: a later approve is an invalid transition, and a second
        // decline is one too (only Pending rows may be resolved).
        await svc.RequestToJoinGroupAsync(g2.Id, requester2);
        await svc.DeclineJoinRequestAsync(g2.Id, requester2, owner);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ApproveJoinRequestAsync(g2.Id, requester2, owner));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeclineJoinRequestAsync(g2.Id, requester2, owner));

        // Declined row: the self-lane withdraw is also refused (the withdraw
        // lane requires a Pending row; a resolved one is an invalid transition).
        await svc.RequestToJoinGroupAsync(g3.Id, requester3);
        await svc.DeclineJoinRequestAsync(g3.Id, requester3, owner);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.WithdrawJoinRequestAsync(g3.Id, requester3));
    }

    // ── GU gate — a supervised child is refused on the approve lane ──────────

    [Fact]
    public async Task Approve_GatedForSupervisedChild()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-sup-owner";
        const string guardian = "u-jr-sup-guardian";
        const string supervisedChild = "u-jr-sup-child";
        const string independent = "u-jr-sup-independent"; // no link

        var gSupervised = await svc.CreateGroupAsync(owner, "Supervised", null);
        await svc.RequestToJoinGroupAsync(gSupervised.Id, supervisedChild);
        await svc.CreateGuardianLinkAsync(supervisedChild, guardian);

        // Supervised: the approve lane is refused (the GU gate).
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ApproveJoinRequestAsync(gSupervised.Id, supervisedChild, owner));

        // Conditional on the active link: a child with NO link approves normally.
        var gIndependent = await svc.CreateGroupAsync(owner, "Independent", null);
        await svc.RequestToJoinGroupAsync(gIndependent.Id, independent);
        await svc.ApproveJoinRequestAsync(gIndependent.Id, independent, owner);
        Assert.Contains(gIndependent.Id, await svc.GetGroupIdsAsync(independent));
    }

    // ── C3 / C-M2·2 — every write audits; the two reads never do ────────────

    [Fact]
    public async Task Every_JoinRequest_Write_Appends_Its_AuditRow_C3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-z1";
        var gA = await svc.CreateGroupAsync(owner, "ZA", null);
        var gB = await svc.CreateGroupAsync(owner, "ZB", null);
        var gC = await svc.CreateGroupAsync(owner, "ZC", null);
        var gD = await svc.CreateGroupAsync(owner, "ZD", null);

        var a = "u-jr-z2";
        var b = "u-jr-z3";
        var c = "u-jr-z4";
        var d = "u-jr-z5";

        await svc.RequestToJoinGroupAsync(gA.Id, a);
        await svc.ApproveJoinRequestAsync(gA.Id, a, owner);

        await svc.RequestToJoinGroupAsync(gB.Id, b);
        await svc.DeclineJoinRequestAsync(gB.Id, b, owner);

        await svc.RequestToJoinGroupAsync(gC.Id, c);
        await svc.WithdrawJoinRequestAsync(gC.Id, c);

        await svc.RequestToJoinGroupAsync(gD.Id, d); // request only

        await using var session = store.QuerySession();
        var actions = await session.Query<AccessAudit>()
            .Where(x => x.TargetKind == "group" && x.Action.StartsWith("group.join"))
            .Select(x => x.Action)
            .ToListAsync(TestContext.Current.CancellationToken);

        // 4 request rows + 1 approve + 1 decline + 1 withdraw = 7 rows, 4
        // distinct actions. The CreateGroupAsync rows on the same target-kind
        // are M1's lane (no "group.join*" prefix) and deliberately outside this
        // assertion.
        Assert.Equal(7, actions.Count);
        var distinct = actions.Distinct().OrderBy(x => x).ToArray();
        Assert.Equal(new[]
        {
            "group.join.approve",
            "group.join.decline",
            "group.join.request",
            "group.join.withdraw",
        }, distinct);
    }

    [Fact]
    public async Task The_Two_JoinRequest_Read_Lanes_Append_No_AuditRow_CM2_2()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-r1";
        const string requester = "u-jr-r2";

        var group = await svc.CreateGroupAsync(owner, "Reads", null);
        await svc.RequestToJoinGroupAsync(group.Id, requester);

        var before = await AuditCountAsync(store);

        // The two read lanes (the candidate-read pin): none may append.
        await svc.GetPendingJoinRequestsForUserAsync(requester);
        await svc.GetPendingJoinRequestsForGroupAsync(group.Id);

        Assert.Equal(before, await AuditCountAsync(store));
    }

    [Fact]
    public async Task The_Pending_Read_Lanes_Filter_And_Order_LiveRows_C4()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-o1";
        const string pending = "u-jr-o2";
        const string resolved = "u-jr-o3";

        var group = await svc.CreateGroupAsync(owner, "Ordering", null);
        await svc.RequestToJoinGroupAsync(group.Id, pending);
        await svc.RequestToJoinGroupAsync(group.Id, resolved);

        await svc.ApproveJoinRequestAsync(group.Id, resolved, owner);

        // User axis: the resolved row dropped, the pending one survives —
        // live on the very next call (C4 on the read lanes).
        var forUser = await svc.GetPendingJoinRequestsForUserAsync(pending);
        Assert.Single(forUser);
        Assert.Equal(JoinRequestStatus.Pending, forUser[0].Status);

        Assert.Empty(await svc.GetPendingJoinRequestsForUserAsync(resolved));

        // Group axis: one pending row remains, for the other requester.
        var forGroup = await svc.GetPendingJoinRequestsForGroupAsync(group.Id);
        Assert.Single(forGroup);
        Assert.Equal(pending, forGroup[0].UserId);

        // Fail-safe: empty ids return empty lists, no throw.
        Assert.Empty(await svc.GetPendingJoinRequestsForUserAsync(string.Empty));
        Assert.Empty(await svc.GetPendingJoinRequestsForGroupAsync(string.Empty));
    }

    // ── DeleteGroupAsync cascade — the group's join requests go with it ──────

    [Fact]
    public async Task DeleteGroup_Cascades_Its_JoinRequest_Rows()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-jr-del-owner";
        const string requester = "u-jr-del-requester";

        var group = await svc.CreateGroupAsync(owner, "Doomed", null);
        await svc.RequestToJoinGroupAsync(group.Id, requester);
        Assert.Single(await svc.GetPendingJoinRequestsForUserAsync(requester));

        await svc.DeleteGroupAsync(group.Id, owner);

        // The request row is gone with the group (no orphan for the requester).
        Assert.Empty(await svc.GetPendingJoinRequestsForUserAsync(requester));
    }

    // ── Shared helpers ──────────────────────────────────────────────────────

    private static async Task<AccessAudit> LastAuditAsync(IDocumentStore store, string action, string targetId)
    {
        await using var session = store.QuerySession();
        var row = await session.Query<AccessAudit>()
            .Where(x => x.Action == action && x.TargetId == targetId)
            .OrderByDescending(x => x.At)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        return row!;
    }

    private static async Task<int> AuditCountAsync(IDocumentStore store)
    {
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .CountAsync(TestContext.Current.CancellationToken);
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
