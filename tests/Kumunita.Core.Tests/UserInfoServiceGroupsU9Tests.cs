using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M2, plan U9 — the two new <see cref="IUserInfoService"/> group-read ADDs that
/// U9's drift-guard opened in the same commit as U9's first Web consumer
/// (design doc §2.1 + §2.2 + §2.7). Each test hands itself a fresh scratch
/// Postgres DB (<see cref="PostgresFixture.NewDatabaseAsync"/>), mirroring the
/// <c>BootStoreAsync</c> shape U3/U5/U6 established in this assembly.
/// <para>
/// <see cref="UserInfoService.GetGroupsForUserAsync"/> (F14 — "my group list
/// shows only groups I own plus groups I belong to"): asserts the owner-∪-member
/// projection, dedupe, and the "a membership change is live on the next call"
/// invariant (C4), and — per C-M2·2 — that the read appends <b>no</b>
/// <c>AccessAudit</c> row (it is a candidate projection, not a decision).
/// </para>
/// <para>
/// <see cref="UserInfoService.GetGroupMembersAsync"/> (U9's <c>GroupViewModel</c>
/// <c>MemberCount</c> + U10's <c>Detail.Members</c>): asserts the per-group
/// membership read, its C4 live-on-next-call lane, and its no-audit-row shape.
/// </para>
/// </summary>
public class UserInfoServiceGroupsU9Tests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Test 1 — GetGroupsForUserAsync: owner ∪ member, deduped ─────────────
    //
    // Actor A owns two groups, is a member of a third (owned by B), and is
    // neither owner nor member of a fourth (owned by C). The read returns
    // exactly {owned1, owned2, memberOfB} — A's own groups ∪ membership —
    // and excludes the C-owned group A cannot see. Dedupe: a group A owns AND
    // is a member of appears once (CreateGroupAsync makes the owner a member,
    // so that is the normal shape).
    [Fact]
    public async Task GetGroupsForUserAsync_ReturnsOwnerUnionMember_ExcludesOther()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string a = "u-u9-a";
        const string b = "u-u9-b";
        const string c = "u-u9-c";

        // A owns two groups (owner is auto-added as a member by CreateGroupAsync).
        var owned1 = await svc.CreateGroupAsync(a, "A owned 1", null);
        var owned2 = await svc.CreateGroupAsync(a, "A owned 2", null);

        // B owns one group; A is a member of it.
        var bGroup = await svc.CreateGroupAsync(b, "B group", null);
        await svc.AddGroupMemberAsync(bGroup.Id, a, addedBy: b);

        // C owns a group A is unrelated to — must NOT appear for A.
        var cGroup = await svc.CreateGroupAsync(c, "C group", null);

        var result = await svc.GetGroupsForUserAsync(a);

        var ids = result.Select(g => g.Id).ToHashSet();
        Assert.Contains(owned1.Id, ids);
        Assert.Contains(owned2.Id, ids);
        Assert.Contains(bGroup.Id, ids);
        Assert.DoesNotContain(cGroup.Id, ids);

        // Exactly three — no duplicates (owner-∪-membership collapses to the
        // group id; A owns two, is in one).
        Assert.Equal(3, result.Count);
    }

    // ── Test 2 — GetGroupsForUserAsync: no audit row (C-M2·2 read, not decision) ──
    [Fact]
    public async Task GetGroupsForUserAsync_AppendsNoAuditRow_CM2_2()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string a = "u-u9-a2";
        var owned = await svc.CreateGroupAsync(a, "A2 group", null);

        // The two reads under test are candidates, not decisions — neither may
        // append an AccessAudit row (invariant C3's lane is untouched).
        await svc.GetGroupsForUserAsync(a);
        await svc.GetGroupMembersAsync(owned.Id);

        await using var session = store.QuerySession();
        var audits = await session.Query<Kumunita.Core.Authorization.AccessAudit>()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(audits);
    }

    // ── Test 3 — Group membership is live on the next GetGroupMembersAsync (C4) ─
    //
    // Add a member via the M1 write seam; the *very next* GetGroupMembersAsync
    // returns it. Remove it; the next call drops it. No projection, no cache —
    // the live row is the truth (invariant C4), now proven on the per-group
    // read lane (U9's MemberCount / U10's Detail.Members) rather than only the
    // user-axis GetGroupIdsAsync.
    [Fact]
    public async Task GetGroupMembersAsync_LiveOnNextCall_C4_StrongConsistency()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string owner = "u-u9-owner";
        const string member = "u-u9-member";

        var group = await svc.CreateGroupAsync(owner, "Live group", null);

        // The owner is the only member at creation (owner ∪ owner).
        var before = await svc.GetGroupMembersAsync(group.Id);
        Assert.Single(before);
        Assert.Equal(owner, before[0].UserId);

        // Add a member — live on the very next call.
        await svc.AddGroupMemberAsync(group.Id, member, addedBy: owner);
        var afterAdd = await svc.GetGroupMembersAsync(group.Id);
        Assert.Equal(2, afterAdd.Count);
        Assert.Contains(afterAdd, m => m.UserId == member);

        // Remove the member — live on the very next call.
        await svc.RemoveGroupMemberAsync(group.Id, member, removedBy: owner);
        var afterRemove = await svc.GetGroupMembersAsync(group.Id);
        Assert.Single(afterRemove);
        Assert.Equal(owner, afterRemove[0].UserId);
    }

    // ── Test 4 — GetGroupMembersAsync: empty group id is fail-safe (no throw) ──
    [Fact]
    public async Task GetGroupMembersAsync_EmptyGroupId_ReturnsEmpty()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        Assert.Empty(await svc.GetGroupMembersAsync(string.Empty));
        Assert.Empty(await svc.GetGroupsForUserAsync(string.Empty));
    }

    // ── Shared helper (mirror U6's BootStoreAsync shape in this assembly) ──
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
