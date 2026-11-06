using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C3-03 — the <see cref="GroupPostService"/> seam's own tests. The 4-method
/// group-post lane (ADR 0013 / ADR 0016) was extracted out of
/// <see cref="PostService"/> as a real part-vs-whole surface (the
/// <see cref="GroupPostService"/> class + <see cref="IGroupPostService"/>
/// interface — the C3-00 <c>EventRsvpGuardianService</c> pattern). These tests
/// construct the seam **directly** (not through the <see cref="PostService"/>
/// forwarders the existing <c>GroupPostServiceTests</c> exercises) to prove the
/// seam is a standalone surface that works on its own: the same
/// <see cref="PostgresFixture"/> (fresh scratch Postgres), the same
/// <c>UserInfoService</c> + <c>AuthorizationService</c> trio, but the group-lane
/// calls go through <see cref="GroupPostService"/> (the named seam) rather than
/// the god-service it came out of.
/// <para>
/// This is the DoD's "each extracted seam has its own unit test" line for the
/// group-post seam (the <c>PostgresFixture</c> harness the C3-00 / C3-01
/// precedents used for DB-dependent seams), and it satisfies the gate's
/// seam-test Rule 2 (<see cref="IGroupPostService"/> named by a test file).
/// </para>
/// </summary>
public class GroupPostServiceSeamTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — the feed: a member sees the group channel (G1) ────────────────
    //
    // A member reads the group channel through the <see cref="GroupPostService"/>
    // seam directly: the post is in <c>Visible</c> and the visit's single
    // aggregate row is present (TargetKind "grouppost", Allow, Via Group).

    [Fact]
    public async Task Seam_ListGroupFeed_MemberSeesChannel()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, authz, svc) = Services(store);
        const string owner = "u-gp-seam-feed-owner";

        var group = await userInfo.CreateGroupAsync(owner, "Seam feed", null);
        await Plant(store, GroupPost("seam-feed-post", group.Id, owner));

        var feed = await svc.ListGroupFeedAsync(group.Id, owner, page: 1);
        var post = Assert.Single(feed.Visible);
        Assert.Equal("seam-feed-post", post.Id);
        Assert.Equal(0, feed.HiddenCount);

        var aggregate = Assert.Single(await GroupPostAudits(store, actor: owner),
            a => a.TargetId is null);
        Assert.Equal(AccessAction.Read.Id, aggregate.Action);
        Assert.Equal("grouppost", aggregate.TargetKind);
        Assert.Equal(AccessVia.Group, aggregate.Via);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);
    }

    // ── 2 — the detail: a member sees the post + replies (G7) ─────────────
    //
    // A member's detail visit through the <see cref="GroupPostService"/> seam:
    // the post is returned with its replies, and the detail decision row
    // (TargetId = postId, Via Group, Allow) is written.

    [Fact]
    public async Task Seam_GetGroupPost_MemberSeesPostAndReplies()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, authz, svc) = Services(store);
        const string owner = "u-gp-seam-detail-owner";

        var group = await userInfo.CreateGroupAsync(owner, "Seam detail", null);
        await Plant(store, GroupPost("seam-detail-post", group.Id, owner));
        await Plant(store, new PostReply
        {
            Id = "seam-detail-reply",
            PostId = "seam-detail-post",
            AuthorId = owner,
            Body = "a reply",
            Created = DateTimeOffset.UtcNow
        });

        var result = await svc.GetGroupPostAsync(group.Id, "seam-detail-post", owner);
        Assert.NotNull(result.Post);
        Assert.Equal("seam-detail-post", result.Post!.Id);
        Assert.Single(result.Replies);
        Assert.Equal("seam-detail-reply", result.Replies[0].Id);

        var row = Assert.Single(await GroupPostAudits(store, actor: owner),
            a => a.TargetId == "seam-detail-post");
        Assert.Equal(AccessVia.Group, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 3 — the create gate: a non-member is denied (G3) ──────────────────
    //
    // A stranger creates through the <see cref="GroupPostService"/> seam: the
    // group-lane gate (<see cref="IAuthorizationService.CanSeeGroupAsync"/>)
    // denies, the gate row commits **before** the throw, and no post is stored.

    [Fact]
    public async Task Seam_CreateGroupPost_NonMemberDenied()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, authz, svc) = Services(store);
        const string owner = "u-gp-seam-create-owner";
        const string stranger = "u-gp-seam-create-stranger";

        var group = await userInfo.CreateGroupAsync(owner, "Seam create", null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => svc.CreateGroupPostAsync(
                new GroupPostDraft(group.Id, "title", "body"), stranger, s)));

        // The gate row committed (TargetId = the group id, the channel)
        // **before** the throw — the seam's create-gate is the same gate the
        // PostService forwarder used to run (G6 FACES, mirrored).
        var gateRow = Assert.Single(await GroupPostAudits(store, actor: stranger),
            a => a.TargetId == group.Id);
        Assert.Equal(AccessOutcome.Deny, gateRow.Outcome);
        Assert.Equal(AccessVia.Group, gateRow.Via);
    }

    // ── 4 — the edit lane: author-only (ADR 0016) ─────────────────────────
    //
    // The author re-writes a group post through the <see cref="GroupPostService"/>
    // seam: title + body updated, the author-only gate holds (a non-author is
    // denied with the lane's failure shape).

    [Fact]
    public async Task Seam_UpdateGroupPost_AuthorOnly()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, authz, svc) = Services(store);
        const string author = "u-gp-seam-edit-author";
        const string stranger = "u-gp-seam-edit-stranger";

        var group = await userInfo.CreateGroupAsync(author, "Seam edit", null);
        await Plant(store, GroupPost("seam-edit-post", group.Id, author));

        // Author succeeds: title + body rewritten.
        var updated = await RunInSession(store, s => svc.UpdateGroupPostAsync(
            "seam-edit-post", author, "new title", "new body", null, s));
        Assert.Equal("new title", updated.Title);
        Assert.Equal("new body", updated.Body);

        // Non-author is denied (the author-only gate, the sole decision on this lane).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => svc.UpdateGroupPostAsync(
                "seam-edit-post", stranger, "hijack", "hijack", null, s)));
    }

    // ── Shared helpers (the C3-00 / C3-01 seam-test shape — the same
    //     PostgresFixture boot + service trio, the group-lane surface
    //     constructed on the named seam rather than the god-service) ────────

    private async Task<(IDocumentStore store, string conn)> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return (store, conn);
    }

    /// <summary>The service trio over the scratch store, with the group-post
    /// lane resolved on the <see cref="GroupPostService"/> seam directly (not
    /// through the <see cref="PostService"/> forwarders) — the C3-03 named-seam
    /// construction the DI registration supplies in the app.</summary>
    private static (UserInfoService User, AuthorizationService Authz, GroupPostService Group)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var group = new GroupPostService(store, authz); // notifications + mentions null (the optional seams; not exercised here)
        return (userInfo, authz, group);
    }

    /// <summary>A planted group post: non-empty <c>GroupId</c>, empty
    /// <c>ComponentId</c>, audience non-null empty (the G·2/G·8 shape the
    /// service's own writer pins).</summary>
    private static Post GroupPost(string id, string groupId, string author) => new()
    {
        Id = id,
        ComponentId = string.Empty,
        GroupId = groupId,
        AuthorId = author,
        Body = "body " + id,
        Audience = new Audience(),
        Created = DateTimeOffset.UtcNow
    };

    /// <summary>Plant a document row directly (fixture seeding, not a service
    /// write seam — the <c>GroupPostServiceTests.Plant</c> precedent).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task<T> RunInSession<T>(IDocumentStore store, Func<IDocumentSession, Task<T>> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        return await action(session);
    }

    /// <summary>The group lane's audit rows only (TargetKind "grouppost").</summary>
    private static async Task<IReadOnlyList<AccessAudit>> GroupPostAudits(IDocumentStore store, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.TargetKind == "grouppost");
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        return await q.ToListAsync(ct);
    }
}
