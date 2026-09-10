using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0009 — the group description's write seam,
/// <see cref="UserInfoService.UpdateGroupDescriptionAsync"/>, pinned at the
/// service layer. Each test hands itself a fresh scratch Postgres DB
/// (<see cref="PostgresFixture.NewDatabaseAsync"/>), mirroring the
/// <c>BootStoreAsync</c> shape the U9 group-ADD tests established in this
/// assembly.
/// <para>
/// The seam is the ADR 0007 new-lane rule's second consumer: a group write
/// lane whose Web standing is owner ∪ GlobalAdmin (the Web gate; the seam
/// itself does not re-gate — ADR 0006-D: Web shapes HTTP, Core decides). The
/// invariants pinned here:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>C4 strong consistency</b> — the new description is live on the very
/// next <c>GetGroupAsync</c> / <c>GetGroupsForUserAsync</c> call.
/// </item>
/// <item>
/// <b>Audit lane (invariant C3)</b> — one <see cref="AccessAudit"/> row per
/// call, action <c>group.update</c>, <c>TargetKind</c> "group",
/// <c>TargetId</c> = the group; <c>Via</c> derived exactly like the other
/// group lanes: <c>updatedBy == Group.OwnerId ⇒ Owner</c>, else
/// <c>Admin</c> (the effective principal folds to the owner on the Owner
/// lane).
/// </item>
/// <item>
/// <b>Fail-safe</b> — an unknown group id throws
/// <see cref="InvalidOperationException"/> (the
/// <c>AddGroupMemberAsync</c>/<c>RemoveGroupMemberAsync</c> lane shape),
/// never a silent no-op.
/// </item>
/// </list>
/// </summary>
public class UserInfoServiceGroupDescriptionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Test 1 — the write is strong-consistency (C4) on both read lanes ──
    //
    // Create a group with a description, update it; the *very next*
    // GetGroupAsync returns the new value (and GetGroupsForUserAsync's
    // projection carries it too). Update to null; the next reads return null
    // (the Web's "blank clears" lane stores null, the seam stores what it is
    // passed — validation is a Web concern).
    [Fact]
    public async Task UpdateGroupDescriptionAsync_LiveOnNextRead_C4_StrongConsistency()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-desc-owner";

        var group = await svc.CreateGroupAsync(owner, "Building 4", "Residents of Building 4");
        Assert.Equal("Residents of Building 4", group.Description);

        // The write is live on the very next single-group read.
        await svc.UpdateGroupDescriptionAsync(
            group.Id, "Residents of Building 4 — all floors", owner);
        var after = await svc.GetGroupAsync(group.Id);
        Assert.Equal("Residents of Building 4 — all floors", after?.Description);

        // …and on the owner ∪ member projection read (the /groups list lane).
        var forUser = await svc.GetGroupsForUserAsync(owner);
        Assert.Equal("Residents of Building 4 — all floors",
            forUser.Single(g => g.Id == group.Id).Description);

        // Null clears (the Web's "blank clears" mapping; C4 on that lane too).
        await svc.UpdateGroupDescriptionAsync(group.Id, null, owner);
        Assert.Null((await svc.GetGroupAsync(group.Id))?.Description);
    }

    // ── Test 2 — owner update: audit row, Via Owner, effective = owner ─────
    [Fact]
    public async Task UpdateGroupDescriptionAsync_ByOwner_AuditViaOwner_C3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-desc-audit-owner";
        var group = await svc.CreateGroupAsync(owner, "Volunteers", null);

        await svc.UpdateGroupDescriptionAsync(group.Id, "Runs the tool library", owner);

        await using var session = store.QuerySession();
        var audits = await session.Query<AccessAudit>()
            .Where(a => a.Action == "group.update" && a.TargetId == group.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Exactly one audit row on this action lane (invariant C3, one row
        // per write, same session).
        var audit = Assert.Single(audits);
        Assert.Equal(owner, audit.ActorId);
        Assert.Equal(AccessVia.Owner, audit.Via);
        Assert.Equal(owner, audit.EffectivePrincipalId);
        Assert.Equal("group", audit.TargetKind);
        Assert.Equal(AccessOutcome.Allow, audit.Outcome);
    }

    // ── Test 3 — non-owner (GlobalAdmin) update: Via Admin ─────────────────
    //
    // The Web gate (TryResolveOwnerSurface) is what admits a non-owner to
    // this lane (ADR 0007); the seam's observable contribution is the audit
    // row's Via: updatedBy != Group.OwnerId ⇒ Admin, effective principal =
    // the actor (no folding to the owner on the Admin lane).
    [Fact]
    public async Task UpdateGroupDescriptionAsync_ByGlobalAdmin_AuditViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-desc-admin-owner";
        const string admin = "u-desc-globaladmin";
        var group = await svc.CreateGroupAsync(owner, "Bike owners", null);

        await svc.UpdateGroupDescriptionAsync(group.Id, "Pump your tires here", admin);

        await using var session = store.QuerySession();
        var audit = await session.Query<AccessAudit>()
            .Where(a => a.Action == "group.update" && a.TargetId == group.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(admin, audit.ActorId);
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(admin, audit.EffectivePrincipalId);

        // The change landed for both reads (C4: the admin's write is the
        // owner's group's visible state).
        Assert.Equal("Pump your tires here", (await svc.GetGroupAsync(group.Id))?.Description);
    }

    // ── Test 4 — unknown group id throws (the lane's fail-safe shape) ──────
    [Fact]
    public async Task UpdateGroupDescriptionAsync_UnknownGroup_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateGroupDescriptionAsync("u-desc-missing-group", "text", "u-desc-any"));

        // No audit row for a write that never landed (invariant C3: the row
        // is committed *with* the change, not for the attempt).
        await using var session = store.QuerySession();
        var audits = await session.Query<AccessAudit>()
            .Where(a => a.Action == "group.update")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(audits);
    }

    // ── Shared helper (mirror U9's BootStoreAsync shape in this assembly) ──
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
