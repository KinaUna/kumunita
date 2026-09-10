using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M2b (docs/design/m2b-group-invitations.md; plan
/// <c>plan-m2b-owner-invited-group-membership-(invite-acceptdecline).md</c>) —
/// the owner-invited group membership lane on
/// <see cref="UserInfoService"/>. Each test hands itself a fresh scratch
/// Postgres DB (<see cref="PostgresFixture.NewDatabaseAsync"/>), mirroring the
/// <c>BootStoreAsync</c> shape <c>UserInfoServiceGroupsU9Tests</c> established
/// in this assembly.
/// <para>
/// Coverage per the plan's test lane: the full lifecycle
/// (invite → accept / decline; cancel) with the invariants pinned in
/// <c>UserInfoServiceGroupInvitationsM2bTests</c>:
/// </para>
/// <list type="bullet">
/// <item>C-M2b·1 — the SoD lane: invite by owner ⇒ <see cref="AccessVia.Owner"/>;
/// invite by a non-owner (GlobalAdmin standing) ⇒ <see cref="AccessVia.Admin"/>;
/// each write appends its <see cref="AccessAudit"/> row in the same
/// transaction (invariant C3) with <c>TargetKind</c> "group".</item>
/// <item>C-M2b·2 — the self-lane: only the invitee (actor == row.UserId) may
/// accept/decline their own row; a foreign caller throws
/// <see cref="InvalidOperationException"/>.</item>
/// <item>C-M2b·3 — the state machine: re-invite resets a resolved row to
/// <c>Pending</c>; double-resolution and cancel-after-resolution throw
/// <see cref="InvalidOperationException"/>.</item>
/// <item>C4 carried — accept makes the membership live on the <b>very next</b>
/// <see cref="IUserInfoService.GetGroupIdsAsync"/> /
/// <see cref="IUserInfoService.GetGroupsForUserAsync"/> call; an invite
/// itself never touches membership.</item>
/// <item>C-M2·2 carried — the three read lanes
/// (<c>GetGroupAsync</c>, <c>GetPendingInvitationsForUserAsync</c>,
/// <c>GetPendingInvitationsForGroupAsync</c>) append no
/// <see cref="AccessAudit"/> row.</item>
/// </list>
/// </summary>
public class UserInfoServiceGroupInvitationsM2bTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Lifecycle 1 — invite, then accept: membership lands ONLY on accept ──

    [Fact]
    public async Task Invite_Then_Accept_Membership_LiveOnNextCall_C4()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-owner";
        const string invitee = "u-m2b-invitee";

        var group = await svc.CreateGroupAsync(owner, "Building 4", null);

        // Invite: the pending row exists, but NO membership yet (C4 lane is
        // accept-only).
        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);
        Assert.DoesNotContain(group.Id, await svc.GetGroupIdsAsync(invitee));
        Assert.DoesNotContain(group.Id, (await svc.GetGroupsForUserAsync(invitee)).Select(g => g.Id));

        // Accept (the self-lane — the invitee resolves their own row): the
        // membership is live on the very next read (C4 carried).
        await svc.AcceptGroupInvitationAsync(group.Id, invitee);
        Assert.Contains(group.Id, await svc.GetGroupIdsAsync(invitee));
        Assert.Contains(group.Id, (await svc.GetGroupsForUserAsync(invitee)).Select(g => g.Id));
    }

    // ── Lifecycle 2 — decline: no membership, row resolved ─────────────────

    [Fact]
    public async Task Invite_Then_Decline_NoMembership_RowResolved()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-a";
        const string invitee = "u-m2b-b";

        var group = await svc.CreateGroupAsync(owner, "Volunteers", null);
        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);

        await svc.DeclineGroupInvitationAsync(group.Id, invitee);

        Assert.DoesNotContain(group.Id, await svc.GetGroupIdsAsync(invitee));

        // The row resolved — it is no longer in the invitee's pending list,
        // nor the group's pending list.
        Assert.Empty(await svc.GetPendingInvitationsForUserAsync(invitee));
        Assert.Empty(await svc.GetPendingInvitationsForGroupAsync(group.Id));
    }

    // ── Lifecycle 3 — cancel: the owner kills a pending row ────────────────

    [Fact]
    public async Task Invite_Then_Cancel_NoMembership_RowResolved()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-c";
        const string invitee = "u-m2b-d";

        var group = await svc.CreateGroupAsync(owner, "Bike owners", null);
        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);

        await svc.CancelGroupInvitationAsync(group.Id, invitee, cancelledBy: owner);

        Assert.DoesNotContain(group.Id, await svc.GetGroupIdsAsync(invitee));
        Assert.Empty(await svc.GetPendingInvitationsForUserAsync(invitee));
        Assert.Empty(await svc.GetPendingInvitationsForGroupAsync(group.Id));
    }

    // ── C-M2b·1 — the SoD lane's via derivation (owner ⇒ Owner, else Admin) ──

    [Fact]
    public async Task Invite_Via_Owner_When_Inviter_Is_GroupOwner()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-e";
        const string invitee = "u-m2b-f";

        var group = await svc.CreateGroupAsync(owner, "Building 12", null);
        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);

        var row = await LastAuditAsync(store, "group.invite", group.Id);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(owner, row.ActorId);
    }

    [Fact]
    public async Task Invite_Via_Admin_When_Inviter_Is_Not_The_Owner()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-g";
        const string admin = "u-m2b-admin";
        const string invitee = "u-m2b-h";

        var group = await svc.CreateGroupAsync(owner, "Building 7", null);
        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: admin);

        var row = await LastAuditAsync(store, "group.invite", group.Id);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(admin, row.ActorId);
        Assert.Equal(admin, row.EffectivePrincipalId);   // not the owner's standing
    }

    [Fact]
    public async Task AcceptAndDecline_AuditRows_Use_The_Invitees_Own_Standing()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-i";
        const string acceptor = "u-m2b-j";
        const string decliner = "u-m2b-k";

        var groupA = await svc.CreateGroupAsync(owner, "A", null);
        var groupD = await svc.CreateGroupAsync(owner, "D", null);

        await svc.InviteGroupMemberAsync(groupA.Id, acceptor, invitedBy: owner);
        await svc.AcceptGroupInvitationAsync(groupA.Id, acceptor);

        await svc.InviteGroupMemberAsync(groupD.Id, decliner, invitedBy: owner);
        await svc.DeclineGroupInvitationAsync(groupD.Id, decliner);

        var acceptRow = await LastAuditAsync(store, "group.invite.accept", groupA.Id);
        Assert.Equal(AccessVia.Owner, acceptRow.Via);   // the invitee's own standing
        Assert.Equal(acceptor, acceptRow.ActorId);
        Assert.Equal(acceptor, acceptRow.EffectivePrincipalId);

        var declineRow = await LastAuditAsync(store, "group.invite.decline", groupD.Id);
        Assert.Equal(AccessVia.Owner, declineRow.Via);
        Assert.Equal(decliner, declineRow.ActorId);
        Assert.Equal(decliner, declineRow.EffectivePrincipalId);
    }

    // ── C-M2b·2 — the self-lane: a foreign caller cannot resolve the row ────

    [Fact]
    public async Task AcceptBy_A_Foreign_Account_Throws_CM2b_2()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-m";
        const string invitee = "u-m2b-n";
        const string stranger = "u-m2b-o";

        var group = await svc.CreateGroupAsync(owner, "Building 3", null);
        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);

        // A stranger (not the row's UserId) cannot accept the invitation.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AcceptGroupInvitationAsync(group.Id, stranger));

        // ... and the row is still pending for the invitee (nothing changed).
        Assert.Single(await svc.GetPendingInvitationsForUserAsync(invitee));

        // Same wall on the decline lane.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeclineGroupInvitationAsync(group.Id, stranger));
        Assert.Single(await svc.GetPendingInvitationsForUserAsync(invitee));
    }

    [Fact]
    public async Task Accept_Unknown_Group_Or_Pair_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-p";
        const string invitee = "u-m2b-q";

        var group = await svc.CreateGroupAsync(owner, "Building 9", null);

        // No invitation row at all for this pair.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AcceptGroupInvitationAsync(group.Id, invitee));

        // A group id that does not exist.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AcceptGroupInvitationAsync("no-such-group", invitee));
    }

    // ── C-M2b·3 — the state machine ─────────────────────────────────────────

    [Fact]
    public async Task ReInvite_After_Decline_Resets_To_Pending_CM2b_3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-r";
        const string invitee = "u-m2b-s";

        var group = await svc.CreateGroupAsync(owner, "Building 5", null);

        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);
        await svc.DeclineGroupInvitationAsync(group.Id, invitee);
        Assert.Empty(await svc.GetPendingInvitationsForUserAsync(invitee));

        // Re-invite: the SAME (group, user) business-key row resets to
        // Pending — no second row, and the invitee is pending once.
        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);
        var pending = await svc.GetPendingInvitationsForUserAsync(invitee);
        Assert.Single(pending);
        Assert.Equal(InvitationStatus.Pending, pending[0].Status);

        // And the self-lane works end-to-end on the re-invited row.
        await svc.AcceptGroupInvitationAsync(group.Id, invitee);
        Assert.Contains(group.Id, await svc.GetGroupIdsAsync(invitee));
    }

    [Fact]
    public async Task Inviting_The_Same_Pair_Twice_Keeps_One_Row_CM2b_3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-t";
        const string invitee = "u-m2b-u";

        var group = await svc.CreateGroupAsync(owner, "Building 6", null);

        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);
        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);

        // One row per (group, user) — the UniqueIndex business key; the
        // re-stamp, not a second row.
        Assert.Single(await svc.GetPendingInvitationsForUserAsync(invitee));
        Assert.Single(await svc.GetPendingInvitationsForGroupAsync(group.Id));
    }

    [Fact]
    public async Task Resolved_Row_Does_Not_Accept_Decline_Or_Cancel_Again_CM2b_3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-v";
        const string invitee1 = "u-m2b-w";
        const string invitee2 = "u-m2b-x";
        const string invitee3 = "u-m2b-y";

        var g1 = await svc.CreateGroupAsync(owner, "G1", null);
        var g2 = await svc.CreateGroupAsync(owner, "G2", null);
        var g3 = await svc.CreateGroupAsync(owner, "G3", null);

        // Accepted row: a second accept is an invalid transition.
        await svc.InviteGroupMemberAsync(g1.Id, invitee1, invitedBy: owner);
        await svc.AcceptGroupInvitationAsync(g1.Id, invitee1);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AcceptGroupInvitationAsync(g1.Id, invitee1));

        // Declined row: a later accept is an invalid transition.
        await svc.InviteGroupMemberAsync(g2.Id, invitee2, invitedBy: owner);
        await svc.DeclineGroupInvitationAsync(g2.Id, invitee2);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AcceptGroupInvitationAsync(g2.Id, invitee2));

        // Cancelled row: a later self-resolution is an invalid transition.
        await svc.InviteGroupMemberAsync(g3.Id, invitee3, invitedBy: owner);
        await svc.CancelGroupInvitationAsync(g3.Id, invitee3, cancelledBy: owner);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeclineGroupInvitationAsync(g3.Id, invitee3));

        // Cancelling a pair that never had a row also throws (invalid lane).
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CancelGroupInvitationAsync(g3.Id, "u-m2b-never", cancelledBy: owner));
    }

    // ── C3 / C-M2·2 — every write audits; the three reads never do ─────────

    [Fact]
    public async Task Every_M2b_Write_Appends_Its_AuditRow_C3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-z1";
        const string a = "u-m2b-z2";
        const string b = "u-m2b-z3";
        const string c = "u-m2b-z4";

        var gA = await svc.CreateGroupAsync(owner, "ZA", null);
        var gB = await svc.CreateGroupAsync(owner, "ZB", null);
        var gC = await svc.CreateGroupAsync(owner, "ZC", null);

        await svc.InviteGroupMemberAsync(gA.Id, a, invitedBy: owner);
        await svc.InviteGroupMemberAsync(gB.Id, b, invitedBy: owner);
        await svc.InviteGroupMemberAsync(gC.Id, c, invitedBy: owner);

        await svc.AcceptGroupInvitationAsync(gA.Id, a);
        await svc.DeclineGroupInvitationAsync(gB.Id, b);
        await svc.CancelGroupInvitationAsync(gC.Id, c, cancelledBy: owner);

        await using var session = store.QuerySession();
        var m2bActions = await session.Query<AccessAudit>()
            .Where(x => x.TargetKind == "group" && x.Action.StartsWith("group.invite"))
            .Select(x => x.Action)
            .ToListAsync(TestContext.Current.CancellationToken);

        // The four m2b action ids are the EXACT set of
        // "group.invite*" rows on the group lane: 3 invitations (one per
        // group) + 1 accept + 1 decline + 1 cancel = 6 rows, 4 distinct
        // actions. The CreateGroupAsync rows on the same target-kind are
        // M1's lane (no "group.invite*" prefix) and deliberately outside
        // this assertion.
        Assert.Equal(6, m2bActions.Count);
        var distinct = m2bActions.Distinct().OrderBy(a => a).ToArray();
        Assert.Equal(new[]
        {
            "group.invite",
            "group.invite.accept",
            "group.invite.cancel",
            "group.invite.decline",
        }, distinct);
    }

    [Fact]
    public async Task The_Three_M2b_Read_Lanes_Append_No_AuditRow_CM2_2()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-r1";
        const string invitee = "u-m2b-r2";

        var group = await svc.CreateGroupAsync(owner, "Reads", null);
        await svc.InviteGroupMemberAsync(group.Id, invitee, invitedBy: owner);

        var before = await AuditCountAsync(store);

        // The three read lanes (plan's C-M2·2 carried pin): none may append.
        await svc.GetGroupAsync(group.Id);
        await svc.GetPendingInvitationsForUserAsync(invitee);
        await svc.GetPendingInvitationsForGroupAsync(group.Id);

        Assert.Equal(before, await AuditCountAsync(store));
    }

    [Fact]
    public async Task The_Pending_Read_Lanes_Filter_And_Order_LiveRows_C4()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-m2b-o1";
        const string pending = "u-m2b-o2";
        const string resolved = "u-m2b-o3";

        var group = await svc.CreateGroupAsync(owner, "Ordering", null);
        await svc.InviteGroupMemberAsync(group.Id, pending, invitedBy: owner);
        await svc.InviteGroupMemberAsync(group.Id, resolved, invitedBy: owner);

        await svc.AcceptGroupInvitationAsync(group.Id, resolved);

        // User axis: the resolved row dropped, the pending one survives —
        // live on the very next call (C4 on the read lanes).
        var forUser = await svc.GetPendingInvitationsForUserAsync(pending);
        Assert.Single(forUser);
        Assert.Equal(InvitationStatus.Pending, forUser[0].Status);

        Assert.Empty(await svc.GetPendingInvitationsForUserAsync(resolved));

        // Group axis: one pending row remains, for the other invitee.
        var forGroup = await svc.GetPendingInvitationsForGroupAsync(group.Id);
        Assert.Single(forGroup);
        Assert.Equal(pending, forGroup[0].UserId);

        // Fail-safe: empty ids return empty lists, no throw.
        Assert.Empty(await svc.GetPendingInvitationsForUserAsync(string.Empty));
        Assert.Empty(await svc.GetPendingInvitationsForGroupAsync(string.Empty));
        Assert.Null(await svc.GetGroupAsync(string.Empty));
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
