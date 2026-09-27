using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0093 — the group delete lane, pinned at the service layer. Each test
/// hands itself a fresh scratch Postgres DB (<see cref="PostgresFixture.NewDatabaseAsync"/>),
/// mirroring the <c>BootStoreAsync</c> shape the group-privacy /
/// group-description tests established in this assembly.
/// <para>
/// The seam covered: <see cref="UserInfoService.DeleteGroupAsync"/> (the
/// owner ∪ GlobalAdmin write lane — the ADR 0007 new-lane rule's next group
/// consumer; the seam does not re-gate, ADR 0006-D, so these tests assert
/// what the seam <em>does</em>: the write + the audit row + C4, not the
/// standing, which the Web gate enforces). The invariants pinned here:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>C4 strong consistency</b> — after the delete the group is gone from
/// every read lane: <c>GetGroupAsync</c> returns null, the owner ∪ member
/// projection drops it, the public-only lane drops it, and
/// <c>GetGroupIdsAsync</c> returns the empty set for the former owner.
/// </item>
/// <item>
/// <b>Hard delete of the membership + invitation rows</b> — the
/// <see cref="GroupMembership"/> and <see cref="GroupInvitation"/> rows for
/// the group are removed in the same transaction (C3), so the group-scoped
/// content becomes unreachable (ADR 0013 membership-only authorization).
/// </item>
/// <item>
/// <b>Audit lane (invariant C3)</b> — one <see cref="AccessAudit"/> row,
/// action <c>group.delete</c>, <c>TargetKind</c> "group", <c>TargetId</c> =
/// the group; <c>Via</c> derived exactly like the other group lanes:
/// <c>deletedBy == Group.OwnerId ⇒ Owner</c>, else <c>Admin</c> (the
/// effective principal folds to the owner on the Owner lane).
/// </item>
/// <item>
/// <b>No cascade</b> — group-scoped <c>Post</c> / <see cref="GroupTranslation"/>
/// rows are left in storage (they become inert because the membership gate is
/// gone, not because they are deleted).
/// </item>
/// <item>
/// <b>Fail-safe</b> — an unknown group id throws
/// <see cref="InvalidOperationException"/> (the other group write lanes'
/// shape), never a silent no-op, and lands no audit row.
/// </item>
/// </list>
/// </summary>
public class UserInfoServiceGroupDeleteTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Test 1 — the delete is strong-consistency (C4) on every read lane ─
    [Fact]
    public async Task DeleteGroupAsync_RemovesGroup_LiveOnNextRead_C4()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-del-owner";
        var group = await svc.CreateGroupAsync(owner, "Building 4", "Residents of Building 4");

        // Sanity: the group is live before the delete.
        Assert.NotNull(await svc.GetGroupAsync(group.Id));
        Assert.Contains(group.Id, (await svc.GetGroupsForUserAsync(owner)).Select(g => g.Id));
        Assert.Contains(group.Id, (await svc.GetPublicGroupsAsync()).Select(g => g.Id));
        Assert.Contains(group.Id, await svc.GetGroupIdsAsync(owner));

        await svc.DeleteGroupAsync(group.Id, owner);

        // The group document is gone (C4: live on the very next single read).
        Assert.Null(await svc.GetGroupAsync(group.Id));

        // …and out of the owner ∪ member projection (the /groups list lane)…
        Assert.DoesNotContain(group.Id, (await svc.GetGroupsForUserAsync(owner)).Select(g => g.Id));

        // …and out of the public-only lane (the grant/access pickers)…
        Assert.DoesNotContain(group.Id, (await svc.GetPublicGroupsAsync()).Select(g => g.Id));

        // …and the owner's group-id set is empty (the membership is gone —
        // the authorization input that the group lane keys on).
        Assert.Empty(await svc.GetGroupIdsAsync(owner));
    }

    // ── Test 2 — the membership + invitation rows are removed (C3) ────────
    [Fact]
    public async Task DeleteGroupAsync_RemovesMembership_AndInvitationRows_C3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-del-mem-owner";
        const string member = "u-del-mem-member";
        var group = await svc.CreateGroupAsync(owner, "Family", "The Smith family");

        // A live membership (owner ∪ added member) + a pending invitation.
        await svc.AddGroupMemberAsync(group.Id, member, owner);
        await svc.InviteGroupMemberAsync(group.Id, "u-del-mem-invitee", owner);

        await using (var before = store.QuerySession())
        {
            var members = await before.Query<GroupMembership>()
                .Where(m => m.GroupId == group.Id)
                .CountAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, members);
            var pending = await before.Query<GroupInvitation>()
                .Where(i => i.GroupId == group.Id)
                .CountAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, pending);
        }

        await svc.DeleteGroupAsync(group.Id, owner);

        await using var after = store.QuerySession();
        var membersAfter = await after.Query<GroupMembership>()
            .Where(m => m.GroupId == group.Id)
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, membersAfter);
        var pendingAfter = await after.Query<GroupInvitation>()
            .Where(i => i.GroupId == group.Id)
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, pendingAfter);
    }

    // ── Test 3 — owner delete: audit row, Via Owner, effective = owner ────
    [Fact]
    public async Task DeleteGroupAsync_ByOwner_AuditViaOwner_C3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-del-audit-owner";
        var group = await svc.CreateGroupAsync(owner, "Family", "The Smith family");

        await svc.DeleteGroupAsync(group.Id, owner);

        await using var session = store.QuerySession();
        var audits = await session.Query<AccessAudit>()
            .Where(a => a.Action == "group.delete" && a.TargetId == group.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        var audit = Assert.Single(audits);
        Assert.Equal(owner, audit.ActorId);
        Assert.Equal(AccessVia.Owner, audit.Via);
        Assert.Equal(owner, audit.EffectivePrincipalId);
        Assert.Equal("group", audit.TargetKind);
        Assert.Equal(AccessOutcome.Allow, audit.Outcome);
    }

    // ── Test 4 — non-owner (GlobalAdmin) delete: Via Admin ────────────────
    //
    // The Web gate (TryResolveOwnerSurface) is what admits a non-owner to
    // this lane (ADR 0007); the seam's observable contribution is the audit
    // row's Via: deletedBy != Group.OwnerId ⇒ Admin, effective principal =
    // the actor (no folding to the owner on the Admin lane).
    [Fact]
    public async Task DeleteGroupAsync_ByGlobalAdmin_AuditViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-del-admin-owner";
        const string admin = "u-del-globaladmin";
        var group = await svc.CreateGroupAsync(owner, "Family", "The Smith family");

        await svc.DeleteGroupAsync(group.Id, admin);

        await using var session = store.QuerySession();
        var audit = await session.Query<AccessAudit>()
            .Where(a => a.Action == "group.delete" && a.TargetId == group.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(admin, audit.ActorId);
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(admin, audit.EffectivePrincipalId);

        // The group is gone for the owner's reads too (C4: the admin's
        // delete removed the shared group document + membership rows).
        Assert.Null(await svc.GetGroupAsync(group.Id));
        Assert.DoesNotContain(group.Id, (await svc.GetGroupsForUserAsync(owner)).Select(g => g.Id));
    }

    // ── Test 5 — no cascade: group-scoped translations survive the delete ─
    //
    // ADR 0093: the group's GroupTranslation rows are *not* deleted; they are
    // left in storage and become unreachable (the membership-only group lane,
    // ADR 0013, no longer admits anyone). The "no hard-delete cascade"
    // posture (ADR 0024 / 0086 / 0087) applied to the group axis.
    [Fact]
    public async Task DeleteGroupAsync_LeavesGroupTranslations_InPlace_NoCascade()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-del-trans-owner";
        var group = await svc.CreateGroupAsync(owner, "Family", "The Smith family");

        // A user-added name/description translation (the ADR 0026 lane).
        await svc.AddGroupTranslationAsync(
            group.Id, "fr", "Famille", "La famille Smith", owner,
            new[] { Roles.Member }.ToHashSet(StringComparer.OrdinalIgnoreCase),
            store.OpenSession(new Marten.Services.SessionOptions()));

        var translationsBefore = await svc.GetGroupTranslationsAsync(group.Id);
        Assert.Single(translationsBefore);

        await svc.DeleteGroupAsync(group.Id, owner);

        // The group is gone, but the translation row it was tied to survives
        // in storage (it is now inert — nothing can read it back through the
        // group lane, since the membership that gated it is removed).
        Assert.Null(await svc.GetGroupAsync(group.Id));
        var translationsAfter = await svc.GetGroupTranslationsAsync(group.Id);
        Assert.Single(translationsAfter);
    }

    // ── Test 6 — unknown group id throws (the lane's fail-safe shape) ─────
    [Fact]
    public async Task DeleteGroupAsync_UnknownGroup_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeleteGroupAsync("u-del-missing-group", "u-del-any"));

        // No audit row for a write that never landed (invariant C3: the row
        // is committed *with* the change, not for the attempt).
        await using var session = store.QuerySession();
        var audits = await session.Query<AccessAudit>()
            .Where(a => a.Action == "group.delete")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(audits);
    }

    // ── Shared helper (mirror the group-privacy tests' BootStoreAsync) ────
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
