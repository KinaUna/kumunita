using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0010 — private groups: the <see cref="Group.IsPrivate"/> flag's two
/// service seams, pinned at the service layer. Each test hands itself a
/// fresh scratch Postgres DB (<see cref="PostgresFixture.NewDatabaseAsync"/>),
/// mirroring the <c>BootStoreAsync</c> shape the group-description tests
/// established in this assembly.
/// <para>
/// The seams covered: <see cref="UserInfoService.CreateGroupAsync"/> (the
/// <c>isPrivate</c> default + explicit value),
/// <see cref="UserInfoService.SetGroupPrivacyAsync"/> (the owner ∪ GlobalAdmin
/// write lane — the ADR 0007 new-lane rule's third group consumer; the seam
/// does not re-gate, ADR 0006-D), and
/// <see cref="UserInfoService.GetPublicGroupsAsync"/> (the public-only read
/// lane both grant/access pickers consume). The invariants pinned here:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>C4 strong consistency</b> — a privacy flip is live on the very next
/// <c>GetGroupAsync</c> / <c>GetGroupsForUserAsync</c> call, and
/// <c>GetPublicGroupsAsync</c> excludes a private group immediately.
/// </item>
/// <item>
/// <b>Audit lane (invariant C3)</b> — one <see cref="AccessAudit"/> row per
/// write, action <c>group.update</c>, <c>TargetKind</c> "group",
/// <c>TargetId</c> = the group; <c>Via</c> derived exactly like the other
/// group lanes: <c>updatedBy == Group.OwnerId ⇒ Owner</c>, else
/// <c>Admin</c> (the effective principal folds to the owner on the Owner
/// lane).
/// </item>
/// <item>
/// <b>Fail-safe</b> — an unknown group id throws
/// <see cref="InvalidOperationException"/> (the
/// <c>UpdateGroupDescriptionAsync</c> lane shape), never a silent no-op.
/// </item>
/// </list>
/// </summary>
public class UserInfoServiceGroupPrivacyTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Test 1 — the write is strong-consistency (C4) on the owner's reads,
    //    and GetPublicGroupsAsync excludes the group the moment it flips to
    //    private (both directions).
    [Fact]
    public async Task SetGroupPrivacyAsync_LiveOnNextRead_C4_StrongConsistency()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-priv-owner";

        // Default: a group created without the flag is public → visible to
        // the picker read lane.
        var group = await svc.CreateGroupAsync(owner, "Building 4", "Residents of Building 4");
        Assert.False(group.IsPrivate);
        Assert.Contains(group.Id, (await svc.GetPublicGroupsAsync()).Select(g => g.Id));

        // Flip to private — live on the very next single-group read…
        await svc.SetGroupPrivacyAsync(group.Id, true, owner);
        Assert.True((await svc.GetGroupAsync(group.Id))!.IsPrivate);

        // …and on the owner ∪ member projection read (the /groups list lane).
        var forUser = await svc.GetGroupsForUserAsync(owner);
        Assert.True(forUser.Single(g => g.Id == group.Id).IsPrivate);

        // …and gone from the public-only read lane (the grant/access pickers).
        Assert.DoesNotContain(group.Id, (await svc.GetPublicGroupsAsync()).Select(g => g.Id));

        // Flip back to public — live again on the picker read lane (C4, round
        // trip).
        await svc.SetGroupPrivacyAsync(group.Id, false, owner);
        Assert.Contains(group.Id, (await svc.GetPublicGroupsAsync()).Select(g => g.Id));
        Assert.False((await svc.GetGroupAsync(group.Id))!.IsPrivate);
    }

    // ── Test 2 — owner update: audit row, Via Owner, effective = owner ─────
    [Fact]
    public async Task SetGroupPrivacyAsync_ByOwner_AuditViaOwner_C3()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-priv-audit-owner";
        var group = await svc.CreateGroupAsync(owner, "Family", "The Smith family", true);

        await svc.SetGroupPrivacyAsync(group.Id, false, owner);

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
    public async Task SetGroupPrivacyAsync_ByGlobalAdmin_AuditViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-priv-admin-owner";
        const string admin = "u-priv-globaladmin";
        var group = await svc.CreateGroupAsync(owner, "Family", "The Smith family");

        await svc.SetGroupPrivacyAsync(group.Id, true, admin);

        await using var session = store.QuerySession();
        var audit = await session.Query<AccessAudit>()
            .Where(a => a.Action == "group.update" && a.TargetId == group.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(admin, audit.ActorId);
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(admin, audit.EffectivePrincipalId);

        // The change landed for both reads (C4: the admin's write is the
        // owner's group's visible state) — and the group is now hidden from
        // the public-only lane.
        Assert.True((await svc.GetGroupAsync(group.Id))!.IsPrivate);
        Assert.DoesNotContain(group.Id, (await svc.GetPublicGroupsAsync()).Select(g => g.Id));
    }

    // ── Test 4 — unknown group id throws (the lane's fail-safe shape) ──────
    [Fact]
    public async Task SetGroupPrivacyAsync_UnknownGroup_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SetGroupPrivacyAsync("u-priv-missing-group", true, "u-priv-any"));

        // No audit row for a write that never landed (invariant C3: the row
        // is committed *with* the change, not for the attempt).
        await using var session = store.QuerySession();
        var audits = await session.Query<AccessAudit>()
            .Where(a => a.Action == "group.update")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(audits);
    }

    // ── Test 5 — GetPublicGroupsAsync returns only the public groups ───────
    //
    // Two public groups + one private, same owner: the picker read lane
    // returns exactly the two publics (the private one is the whole point of
    // the feature — it must not appear). Membership is asserted as a set, not
    // a strict order, because groups created here all carry the default
    // Created value and the Created-desc tie-break is not what this test pins.
    [Fact]
    public async Task GetPublicGroupsAsync_ExcludesPrivateGroups_IncludesPublics()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-priv-list-owner";
        _ = await svc.CreateGroupAsync(owner, "Mushroom hunters", null);       // public
        _ = await svc.CreateGroupAsync(owner, "Bike owners", null);            // public (default)
        var privateFam = await svc.CreateGroupAsync(owner, "The family", null, true);
        _ = await svc.CreateGroupAsync(owner, "A public too", null, false);    // public (explicit)

        var names = (await svc.GetPublicGroupsAsync())
            .Select(g => g.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(
            new[] { "A public too", "Bike owners", "Mushroom hunters" },
            names);
        Assert.DoesNotContain("The family", names);

        // Sanity: the private group still exists and is visible to its owner
        // via the /groups list lane — only the public-only lane hides it.
        Assert.Contains(privateFam.Id, (await svc.GetGroupsForUserAsync(owner)).Select(g => g.Id));
    }

    // ── Test 6 — CreateGroupAsync honors the isPrivate default and explicit ─
    //
    // The default parameter (false → public) keeps the ADR 0006-E source
    // extension backward-compatible for every existing 3-arg caller; the
    // explicit true produces a private group (the family use case).
    [Fact]
    public async Task CreateGroupAsync_HonorsIsPrivateDefault_AndExplicitTrue()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-priv-create";

        var byDefault = await svc.CreateGroupAsync(owner, "By default", null);
        Assert.False(byDefault.IsPrivate);

        var explicitPrivate = await svc.CreateGroupAsync(owner, "Explicit private", null, true);
        Assert.True(explicitPrivate.IsPrivate);
        // …and it is private on the very next read (persistence, not a
        // transient field).
        Assert.True((await svc.GetGroupAsync(explicitPrivate.Id))!.IsPrivate);
    }

    // ── Shared helper (mirror the group-description tests' BootStoreAsync) ──
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
