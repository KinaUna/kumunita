using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0226 — <see cref="PostService.CountRepliesAsync"/>, the batched read behind
/// the feed card footer's reply count. Kept in its own file because
/// <c>PostServiceTests</c> pins its test names (§2.6 drift-guard).
/// </summary>
public class PostReplyCountTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task CountRepliesAsync_EmptyInput_ReturnsEmpty()
    {
        var (_, svc) = await BootAsync();
        Assert.Empty(await svc.CountRepliesAsync([]));
    }

    [Fact]
    public async Task CountRepliesAsync_CountsPerPost_IncludingSoftDeleted_AndOmitsPostsWithoutReplies()
    {
        var (store, svc) = await BootAsync();
        await Plant(store, Reply("r1", "p-a"));
        await Plant(store, Reply("r2", "p-a"));
        await Plant(store, new PostReply
        {
            Id = "r3", PostId = "p-a", AuthorId = "u1", Body = "gone",
            Created = DateTimeOffset.UtcNow, DeletedAt = DateTimeOffset.UtcNow,
        });
        await Plant(store, Reply("r4", "p-b"));
        await Plant(store, Reply("r5", "p-other"));

        var counts = await svc.CountRepliesAsync(["p-a", "p-b", "p-none"]);

        Assert.Equal(3, counts["p-a"]);   // soft-deleted still counts (ADR 0024)
        Assert.Equal(1, counts["p-b"]);
        Assert.False(counts.ContainsKey("p-none"));   // absent = 0
        Assert.False(counts.ContainsKey("p-other"));  // not requested
    }

    private static PostReply Reply(string id, string postId) => new()
    {
        Id = id, PostId = postId, AuthorId = "u1", Body = "hi", Created = DateTimeOffset.UtcNow,
    };

    private async Task<(IDocumentStore Store, PostService Svc)> BootAsync()
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
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        return (store, new PostService(userInfo, authz, store));
    }

    private static async Task Plant(IDocumentStore store, object document)
    {
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
